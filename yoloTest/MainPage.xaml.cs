using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;


namespace yoloTest
{
    public partial class MainPage : ContentPage
    {
        private YoloVisionService _visionService = new YoloVisionService();
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
                ResultLabel.Text = "Camera is started, ready to analyze images...";

#if IOS
                bool isLoaded = _visionService.LoadModel();
                if(isLoaded)
                {
                    ResultLabel.Text = "Model Loaded Successfully, ready to stitch together camera footage";
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
