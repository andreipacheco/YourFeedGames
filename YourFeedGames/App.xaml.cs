using OneSignalSDK.DotNet;
using OneSignalSDK.DotNet;
using OneSignalSDK.DotNet.Core;
using OneSignalSDK.DotNet.Core.Debug;
using Pagamentos;


namespace YourFeedGames
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // DOC: https://documentation.onesignal.com/docs/net-sdk-setup

            string oneSignalAppId = Secrets.OneSignalAppId;

            // Enable verbose OneSignal logging to debug issues if needed.
            OneSignal.Debug.LogLevel = LogLevel.VERBOSE;

            // OneSignal Initialization
            OneSignal.Initialize(oneSignalAppId);

            // RequestPermissionAsync will show the notification permission prompt.
            // We recommend removing the following code and instead using an In-App Message to prompt for notification permission (See step 5)
            OneSignal.Notifications.RequestPermissionAsync(true);

            // Verifica se está inscrito
            if (!OneSignal.User.PushSubscription.OptedIn)
            {
                OneSignal.User.PushSubscription.OptIn();
            }
            string pushId = OneSignal.User.PushSubscription.Id;

            Console.WriteLine($"[OneSignal] Push ID: {pushId}");
            Console.WriteLine($"[OneSignal] Inscrição: {OneSignal.User.PushSubscription.OptedIn}");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}