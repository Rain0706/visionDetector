using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;


namespace yoloTest
{
    public partial class MainPage : ContentPage
    {
        private YoloVisionService _visionService = new YoloVisionService();
        //int count = 0;

#if IOS
        private Platforms.iOS.CameraStreamManager? _cameraStream;
#endif

        public MainPage()
        {
            InitializeComponent();
            //register the AI detection, when there's a result, update the UI
            _visionService.OnDetectionResult = (resultMessage) =>
            {
                //force a return to the main thread to update the UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ResultLabel.Text = resultMessage;
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
                    ResultLabel.Text = "Model Loaded Successfully, ready to stitch together camera footage";
                    // initialize and start the camera stream
                    if(_cameraStream == null)
                    {
                        _cameraStream = new Platforms.iOS.CameraStreamManager(_visionService);
                        // run the camera stream on a background thread to avoid blocking the UI
                        Task.Run(() => 
                        {
                           _cameraStream.StartStream(); 
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
