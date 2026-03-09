using System.Diagnostics;
#if IOS
//using Foundation;
#endif

namespace yoloTest
{
    public partial class MainPage : ContentPage
    {
        //int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }
        private async void OnStartDetectionClicked(object sender, EventArgs e)
        {
            // 檢查並請求相機權限
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status == PermissionStatus.Granted)
            {
                // 這裡可以開始實作影像處理邏輯
                ResultLabel.Text = "相機已啟動，準備分析影像...";
            }
            else
            {
                await DisplayAlert("錯誤", "需要相機權限才能辨識障礙物", "OK");
            }
        }

        //        private async void OnTestPerformanceClicked(object sender, EventArgs e)
        //        {
        //            try
        //            {
        //                // call the phone's built-in camera to take a picture
        //                var photo = await MediaPicker.Default.CapturePhotoAsync();
        //                if (photo == null) return;

        //                ResultLabel.Text = "Photo capture successful, AI inference in progress";

        //                //read the data stream in the picture
        //                using var stream = await photo.OpenReadAsync();

        //#if IOS
        //                // --- 以下是純 iOS 專屬的底層程式碼 ---

        //                // 將照片轉換為 iOS 專用的 CIImage 格式
        //                var imageData = Foundation.NSData.FromStream(stream);
        //                var ciImage = new CoreImage.CIImage(imageData);

        //                // 尋找我們剛剛放進去的模型套件包
        //                var packageUrl = Foundation.NSBundle.MainBundle.GetUrlForResource("yolov8n", "mlpackage");
        //                if (packageUrl == null)
        //                {
        //                    ResultLabel.Text = "錯誤：找不到 yolov8n.mlpackage 模型檔案。";
        //                    return;
        //                }

        //                // 使用 iPhone 的晶片即時編譯模型！(使用非淘汰的 async 版本)
        //                NSUrl compiledUrl = null;
        //                NSError compileErr = null;
        //                await CoreML.MLModel.CompileModelAsync(packageUrl).ContinueWith(t =>
        //                {
        //                    if (t.Exception != null)
        //                    {
        //                        compileErr = NSError.FromDomain(new NSString("CompileModelAsync"), -1);
        //                    }
        //                    else
        //                    {
        //                        compiledUrl = t.Result;
        //                    }
        //                });

        //                if (compileErr != null)
        //                    throw new Exception(compileErr.LocalizedDescription);

        //                // 載入編譯好的模型，並轉換為視覺處理專用的 VNCoreMLModel
        //                var mlModel = CoreML.MLModel.Create(compiledUrl, out var createErr);
        //                var visionModel = Vision.VNCoreMLModel.FromMLModel(mlModel, out var vnErr);

        //                // 建立一個變數來儲存抓到的物件數量
        //                int detectedObjectCount = 0;

        //                // 建立辨識請求
        //                var request = new Vision.VNCoreMLRequest(visionModel, (req, error) =>
        //                {
        //                    // 解析模型回傳的結果 (Bounding Boxes)
        //                    var results = req.GetResults<Vision.VNRecognizedObjectObservation>();
        //                    if (results != null)
        //                    {
        //                        detectedObjectCount = results.Length;
        //                    }
        //                });

        //                // 設定影像處理器
        //                var handler = new Vision.VNImageRequestHandler(ciImage, new Vision.VNImageOptions());

        //                // 開始嚴格計時
        //                var stopwatch = Stopwatch.StartNew();

        //                // 執行神經網路推論
        //                handler.Perform(new Vision.VNRequest[] { request }, out var handlerErr);

        //                // 停止計時
        //                stopwatch.Stop();
        //                var ms = stopwatch.ElapsedMilliseconds;

        //                ResultLabel.Text = $"⚡ 推論完成！\n耗時: {ms} 毫秒\n偵測到: {detectedObjectCount} 個物件";
        //#else
        //            ResultLabel.Text = "此效能測試僅支援 iOS 實機環境。";
        //#endif
        //            }
        //            catch (Exception ex)
        //            {
        //                ResultLabel.Text = $"發生錯誤 : {ex.Message}";
        //            }
        //        }
    }
}
