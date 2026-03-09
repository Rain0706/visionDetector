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

    }
}
