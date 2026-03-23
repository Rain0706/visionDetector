using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;


namespace yoloTest
{
    public partial class MainPage : ContentPage
    {
        private YoloVisionService _visionService = new YoloVisionService();
        private DetectionDrawable _detectionDrawable = new DetectionDrawable();
        //create a drawable to store the detection results and draw them on the screen
        //int count = 0;

        //record the time of last Speech
        private DateTime _lastSpeechTime = DateTime.MinValue;

#if IOS
        private Platforms.iOS.CameraStreamManager? _cameraStream;
#endif

        public MainPage()
        {
            InitializeComponent();
            // set the drawable engine to XAML canvas view
            BoundingBoxCanvas.Drawable = _detectionDrawable;
            //register the AI detection, when there's a result, update the UI
            _visionService.OnDetectionResult = (boxes) =>
            {
                //force a return to the main thread to update the UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    //give the newesst list to Drawable Engine
                    _detectionDrawable.Boxes = boxes;
                    //force the canvas refresh immediately
                    BoundingBoxCanvas.Invalidate();

                    if (boxes.Count > 0)
                    {
                        ResultLabel.Text = $"{boxes.Count} object(s) detected";

                        //check if the time of last Speech is over 3 sec
                        if((DateTime.Now - _lastSpeechTime).TotalSeconds > 5)
                        {
                            //Update the last Speech time to current time
                            _lastSpeechTime = DateTime.Now;

                            //count the amount of each class detected
                            int vehicalCount = boxes.Count(b => b.ClassName == "vehicle");
                            int pedestrianCount = boxes.Count(b => b.ClassName == "pedestrian");
                            int potholeCount = boxes.Count(b => b.ClassName == "pothole");

                            string warningText = "注意前方：";
                            if (vehicalCount > 0) { warningText += $"{vehicalCount}輛車"; }
                            if (pedestrianCount > 0) { warningText += $"{pedestrianCount}位行人"; }
                            if (potholeCount > 0) {warningText += $"{potholeCount}個坑洞"; }

                            //danger approach and vibration feedback
                            bool isDangerouslyClose = boxes.Any(b =>
                                (b.ClassName == "vehicle" || b.ClassName == "pothole") &&
                                (b.Width * b.Height > 0.20f));

                            if (isDangerouslyClose)
                            {
                                // modify the warning text to indicate the danger
                                warningText = "警告！距離極近：" + warningText;
                                // trigger the vibration for iOS
                                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                            }

                            Task.Run(async () =>
                            {
                                // get the list of available locales for text-to-Speech
                                var locales = await TextToSpeech.Default.GetLocalesAsync();
                                // find the zh-TW or any chinese locales in the list
                                var chineseLocale = locales.FirstOrDefault(l =>
                                    (l.Language != null && l.Language.Contains("zh")) ||
                                    (l.Country != null && l.Country.Contains("TW")));
                                // set the speech options
                                var speechOptions = new SpeechOptions()
                                {
                                    Locale = chineseLocale,
                                    Volume = 1.0f //ensure the volume is at maximum
                                };

                                await TextToSpeech.Default.SpeakAsync(warningText, speechOptions);
                            });
                        }
                    }
                    else
                    {
                        ResultLabel.Text = "Environment is safe";
                    }
                    
                });
            };
        }
        private async void OnStartDetectionClicked(object sender, EventArgs e)
        {
            // Check and request camera permissions
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            } 

            if (status == PermissionStatus.Granted)
            {
                // Start the camera stream and set up the vision service
                ResultLabel.Text = "Camera is started, ready to analyze images...";

#if IOS
                bool isLoaded = _visionService.LoadModel();
                if(isLoaded)
                {
                    ResultLabel.Text = "Model Loaded Successfully, ready to stitch together the camera footage";
                    // initialize and start the camera stream
                    if(_cameraStream == null)
                    {
                        _cameraStream = new Platforms.iOS.CameraStreamManager(_visionService);
                        // run the camera stream on a background thread to avoid blocking the UI
                        Task.Run(() => 
                        {
                           _cameraStream.StartStream(); 

                           MainThread.BeginInvokeOnMainThread(() =>
                           {
                                //get the native iOS view for the camera preview and add it to ContentView
                                var uiView = CameraContainer.Handler?.PlatformView as UIKit.UIView;
                                if(uiView != null && _cameraStream.Session != null)
                                {
                                    //get the length and width of the iPhone's screen
                                    var screenBounds = UIKit.UIScreen.MainScreen.Bounds; 
                                    var previewLayer = new AVFoundation.AVCaptureVideoPreviewLayer(_cameraStream.Session)
                                    {
                                        Frame = uiView.Bounds,
                                        VideoGravity = AVFoundation.AVLayerVideoGravity.ResizeAspectFill
                                    };
                                    // insert the preview layer into UI container
                                    uiView.Layer.AddSublayer(previewLayer);
                                    Console.WriteLine("Camera's preview layer already added in the screen");
                                }
                                else
                                {
                                    Console.WriteLine("uiView or Session is null, cannot draw the screen");
                                }
                           });
                        });
                        
                        
                    }
                }
                else
                {
                    ResultLabel.Text = "Model Loading Failed";
                }
#else
                ResultLabel.Text = "CoreML model loading is only implemented for iOS in this example.";

#endif      
            }
            else
            {
                await DisplayAlert("Error", "Camera permissions are required to identify obstacles", "OK");
            }
        }

    }
}
