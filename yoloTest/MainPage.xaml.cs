using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using CommunityToolkit.Maui.Media;
using System.Globalization;

#if IOS
using AVFoundation;
using Foundation;
using MediaPlayer; // for volume button detection
using UIKit; // for volume button detection
#endif

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

        private int _shakeCount = 0; // to count the number of shakes detected
        // to record the time of the first shake detected
        private DateTime _firstShakeTime = DateTime.MinValue;
        // to record the time of the last shake detected
        private DateTime _lastSosTime = DateTime.MinValue;
        // to cancel the speech recognition safely
        private CancellationTokenSource? _speechCts;
        
        private string _accumulatedSpeech = "";

        private int _buttonPressCount = 0; // 音量鍵求救變數
        private DateTime _firstPressTime = DateTime.MinValue;
        private bool _isProgrammaticChange = false; // 用來區分是程式改變音量還是使用者按下音量鍵
#if IOS
    private IDisposable? _volumeObserver;
    private MPVolumeView? _hiddenVolumeView; // hide the volume slider that appears when we capture the volume button events
    private UISlider? _volumeSlider;
#endif
        protected override void OnAppearing()
        {
            base.OnAppearing();
            // 捕捉iOS底層的音量變化
#if IOS
            try
            {
                // 1. 建立一個隱藏的音量控制器 (移出螢幕外)，這能隱藏系統的音量彈窗
                _hiddenVolumeView = new MPVolumeView(new CoreGraphics.CGRect(-100, -100, 10, 10)); 
                var currentWindow = UIApplication.SharedApplication.KeyWindow;
                currentWindow?.AddSubview(_hiddenVolumeView);

                // 2. 找出裡面的 UISlider，用來強制控制音量
                foreach (var view in _hiddenVolumeView.Subviews)
                {
                    if (view is UISlider slider)
                    {
                        _volumeSlider = slider;
                        _volumeSlider.Value = 0.7f; // 初始化設定為 60%
                        break;
                    }
                }
                var audioSession = AVAudioSession.SharedInstance();
                audioSession.SetActive(true, out _); //喚醒audio system
                _volumeObserver = audioSession.AddObserver("outputVolume", NSKeyValueObservingOptions.New, (change) =>
                {
                    // get the current volume level
                    float currentVol = audioSession.OutputVolume;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        OnVolumeButtonTapped(currentVol); // send the current volume to the handler function
                    });
                });
            }
            catch(Exception ex)
            {
                Console.WriteLine($"音量捕捉失敗：{ex.Message}");
            }
#endif
            //if(Accelerometer.Default.IsSupported && !Accelerometer.Default.IsMonitoring)
            //{
            //    Accelerometer.Default.Start(SensorSpeed.UI);
            //    //Accelerometer.Default.ShakeDetected += OnShakeDetected;
            //}
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // 離開畫面，釋放監聽
#if IOS
            _volumeObserver?.Dispose();
            _volumeObserver = null;
            _hiddenVolumeView.RemoveFromSuperview();
#endif
            //if(Accelerometer.Default.IsSupported && Accelerometer.Default.IsMonitoring)
            //{
            //    //Accelerometer.Default.ShakeDetected -= OnShakeDetected;
            //    Accelerometer.Default.Stop();
            //}
        }

#if IOS
        private Platforms.iOS.CameraStreamManager? _cameraStream;
#endif
        private void OnVolumeButtonTapped(float currentVol)
        {
            if (_isProgrammaticChange)
            {
                _isProgrammaticChange = false; // reset the flag
                return;
            }
            //若距離第一次按下超過3秒則重計數(防誤觸)
            if((DateTime.Now - _firstPressTime).TotalSeconds > 3) 
            {
                _buttonPressCount = 0;
                _firstPressTime = DateTime.Now;
            }

            _buttonPressCount++;
            Console.WriteLine($"音量鍵被按下:{_buttonPressCount}次");
            //if pressCounts accumulate 3 times within 3 seconds
            if(_buttonPressCount >= 3)
            {
                _buttonPressCount = 0; //觸發後將counter歸零

                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                Task.Run(async () =>
                {
                    await TextToSpeech.Default.SpeakAsync("啟動按鍵求救");
                    
                });
                ExecuteEmergencySOS();
            }
#if IOS
            // 觸發後，強制把系統音量拉回60%，確保下次按壓有效 (because I found that if the volume is 100%, then it will not count whether I press the volume button)
            if (_volumeSlider != null && (currentVol > 0.85f || currentVol <0.15f))
            {
                // 用 MainThread 確保 UI 更新不會衝突
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    //// 告訴系統：下一步是我程式自己要改的，你等一下不要計數！
                    _isProgrammaticChange = true;
                    _volumeSlider.Value = 0.7f;
                });
            }
#endif
        }

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

        //private async void OnShakeDetected(object? sender, EventArgs e)
        //{
        //    // if the less than 30 seconds have passed since the last successful SOS, ignore it
        //    if ((DateTime.Now - _lastSosTime).TotalSeconds < 30) return;
        //    //if it is the first shake or more than 2 seconds have passed since last shake, reset the shake count
        //    if(_shakeCount ==0 || (DateTime.Now - _firstShakeTime).TotalSeconds > 2)
        //    {
        //        _shakeCount = 1;
        //        _firstShakeTime = DateTime.Now;
        //    }
        //    else
        //    {
        //        _shakeCount++;
        //    }
        //    if(_shakeCount >= 3)
        //    {
        //        // trigger successful, reset the counter and record the time of last successful SOS
        //        _shakeCount = 0;
        //        _lastSosTime = DateTime.Now;
        //    }
        //        MainThread.BeginInvokeOnMainThread(async () =>
        //        {
        //            try
        //            {
        //                // vibrate and speak to tell the user that the emergency is being activated
        //                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        //                await TextToSpeech.Default.SpeakAsync("啟動緊急求救，正在取得您的位置。");
        //                // get the current location of the user
        //                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
        //                var location = await Geolocation.Default.GetLocationAsync(request);

        //                if (location != null)
        //                {
        //                    // combine the latitude and longitude into a google maps link
        //                    string mapsLink = $"https://maps.google.com/?q={location.Latitude},{location.Longitude}";
        //                    string messageText = $"緊急求救！我現在的位置是：{mapsLink}";
        //                    //call the SMS API to send the message to the emergency contact
        //                    var message = new SmsMessage(messageText, new[] { "0989139581" });
        //                    await Sms.Default.ComposeAsync(message);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Console.WriteLine($"求救發送失敗:{ex.Message}");
        //                await TextToSpeech.Default.SpeakAsync("求救發送失敗，請撥打110或確認是否開啟權限。");
        //            }
        //        });
            
        //}
        private async void ExecuteEmergencySOS()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);
                
                if(location != null)
                {
                    string mapsLink = $"https://maps.google.com/?q={location.Latitude},{location.Longitude}";
                    string messageText = $"緊急求救！我現在的位置是：{mapsLink}";

                    //將中文字與網址進行 URL 編碼，避免亂碼或解析錯誤
                    string encodedText = Uri.EscapeDataString(messageText);

                    //var message = new SmsMessage(messageText, new[] { "0989139581" });
                    //await Clipboard.Default.SetTextAsync(messageText); //原本使用剪貼簿，改用直接傳參數進捷徑
                    //await Sms.Default.ComposeAsync(null);

                    // 2. 開啟捷徑 App (名稱叫 "AutoSend")
                    //不用剪貼簿，直接把messageText傳給捷徑
                    
                    var uri = new Uri($"shortcuts://x-callback-url/run-shortcut?name=AutoSend&input=text&text={encodedText}&x-success=visionDetector://");
                    await Launcher.Default.OpenAsync(uri);

                    //await Clipboard.Default.SetTextAsync(null);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"求救發送失敗:{ex.Message}");
                await TextToSpeech.Default.SpeakAsync("求救發送失敗，請撥打110或確認是否開啟權限。");
            }
        }
        //private async void OnScreenDoubleTapped(object sender, TappedEventArgs e)
        //{
        //    //vibrate to give feedback that the phone is starting to listen for the SOS signal
        //    HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
        //    var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        //    if (micStatus != PermissionStatus.Granted)
        //    {
        //        micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
        //    }
        //    // check if the app has permission to access the microphone
        //    var isSpeechGranted = await SpeechToText.Default.RequestPermissions(CancellationToken.None);
        //    if (micStatus != PermissionStatus.Granted || !isSpeechGranted)
        //    {
        //        await TextToSpeech.Default.SpeakAsync("未取得語音權限");
        //        return;
        //    }
        //    try
        //    {
        //        _accumulatedSpeech = ""; //clean the string
        //        if (_speechCts != null && !_speechCts.IsCancellationRequested)
        //        {
        //            _speechCts.Cancel();
        //        }
        //        _speechCts = new CancellationTokenSource();
        //        ////cancel the previous subscription to avoid multiple triggers
        //        //SpeechToText.Default.RecognitionResultCompleted -= OnSpeechComplted;
        //        ////subscribe to the speech recognition event
        //        //SpeechToText.Default.RecognitionResultCompleted += OnSpeechComplted;
        //        // cancel the previous subscription to avoid multiple triggers
        //        SpeechToText.Default.RecognitionResultUpdated -= OnSpeechUpdated;
        //        //subscribe to the speech recognition event
        //        SpeechToText.Default.RecognitionResultUpdated += OnSpeechUpdated;

        //        var options = new SpeechToTextOptions
        //        {
        //            Culture = CultureInfo.GetCultureInfo("zh-TW"), // set the recognition language to Chinese
        //            ShouldReportPartialResults = true // converting while talking
        //        };
        //        await TextToSpeech.Default.SpeakAsync("請說"); //let the system say 請說
        //        // start listening for speech input
        //        await SpeechToText.Default.StartListenAsync(options, _speechCts.Token);
        //        // wait for 4 seconds to get the speech recognition result
        //        await Task.Delay(4000);
        //        if (_speechCts != null && !_speechCts.IsCancellationRequested)
        //        {
        //            _speechCts.Cancel();

        //            await SpeechToText.Default.StopListenAsync(CancellationToken.None);
        //            SpeechToText.Default.RecognitionResultUpdated -= OnSpeechUpdated;
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"啟動語音辨識錯誤: {ex.Message}");
        //    }
        //}
        //private void OnSpeechComplted(object? sender, SpeechToTextRecognitionResultCompletedEventArgs e)
        //{
        //    // afte geting the result, cancel the microphone access and ready to next tap
        //    SpeechToText.Default.RecognitionResultCompleted -= OnSpeechComplted;

        //    MainThread.BeginInvokeOnMainThread(async () =>
        //    {
        //        if (e.RecognitionResult.IsSuccessful && !string.IsNullOrWhiteSpace(e.RecognitionResult.Text))
        //        {
        //            string recognizedText = e.RecognitionResult.Text;
        //            Console.WriteLine($"聽到:{recognizedText}");

        //            if (recognizedText.Contains("救命") || recognizedText.Contains("危險") || recognizedText.Contains("跌倒"))
        //            {
        //                await TextToSpeech.Default.SpeakAsync("收到求救指令，正在發送定位。");
        //                ExecuteEmergencySOS();
        //            }
        //            else
        //            {
        //                await TextToSpeech.Default.SpeakAsync("系統正常運作中。");
        //            }
        //        }
        //    });

        //}
        //private void OnSpeechUpdated(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs e)
        //{
        //    //if (e.RecognitionResult != null && !string.IsNullOrWhiteSpace(e.RecognitionResult))
        //    if(!string.IsNullOrWhiteSpace(e.RecognitionResult))
        //    {
        //        _accumulatedSpeech += e.RecognitionResult;
        //        Console.WriteLine($"碎片重組：{_accumulatedSpeech}");
        //        //string text = e.RecognitionResult;
        //        //Console.WriteLine($"[即時聽到(zh-TW)]: {text}"); // 你可以在 Visual Studio 輸出視窗看它即時打字
        //        bool isEmergency =
        //            (_accumulatedSpeech.Contains("救") && _accumulatedSpeech.Contains("命")) ||
        //            (_accumulatedSpeech.Contains("危") && (_accumulatedSpeech.Contains("險") || _accumulatedSpeech.Contains("险"))) ||
        //            (_accumulatedSpeech.Contains("跌") && _accumulatedSpeech.Contains("倒"));
        //        // 只要一聽到這幾個關鍵字，不用等 4 秒，立刻觸發！
        //        //if (text.Contains("救命") || text.Contains("危险")|| text.Contains("危險") || text.Contains("跌倒"))
        //        if(isEmergency)
        //        {
        //            // 1. 立刻解除監聽並關閉麥克風
        //            SpeechToText.Default.RecognitionResultUpdated -= OnSpeechUpdated;
        //            if (_speechCts != null && !_speechCts.IsCancellationRequested)
        //            {
        //                _speechCts.Cancel();
        //                SpeechToText.Default.StopListenAsync(CancellationToken.None);
        //            }

        //            // 2. 回到主執行緒觸發求救
        //            MainThread.BeginInvokeOnMainThread(async () =>
        //            {
        //                await TextToSpeech.Default.SpeakAsync("收到求救指令");
        //                ExecuteEmergencySOS();
        //            });
        //        }
        //    }
        //}
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

        private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {

        }
    }
}
