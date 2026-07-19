using KrakenMobile.Services;

namespace KrakenMobile;

public partial class App : Application
{
    private static bool _securityPassed;
    private static bool _updateChecked;
    private static TaskCompletionSource? _securityBlocker;

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.Created += async (s, e) =>
        {
            await RunSecurityPipelineAsync(window);
        };

        return window;
    }

    private static async Task RunSecurityPipelineAsync(Window window)
    {
        try
        {
            var protection = ProtectionService.Check();
            if (protection.IsTampered)
            {
                SilentShutdown();
                return;
            }

            ProtectionService.LockSignature();
            ProtectionService.Initialize();

            var integrityResult = await IntegrityService.CheckAsync();
            if (integrityResult == IntegrityResult.Tampered)
            {
                SilentShutdown();
                return;
            }

            _securityPassed = true;

            var updateResult = await UpdateService.CheckForUpdateAsync();
            if (updateResult.Severity == UpdateSeverity.Hard)
            {
                _updateChecked = true;
                var forcePage = new Views.ForceUpdatePage(
                    updateResult.LatestVersion,
                    updateResult.CurrentVersion,
                    updateResult.ReleaseNotes,
                    updateResult.DownloadUrl);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    window.Page = new NavigationPage(forcePage);
                });
                return;
            }

            _updateChecked = true;
        }
        catch
        {
            SilentShutdown();
        }
    }

    private static void SilentShutdown()
    {
        try
        {
            ProtectionService.StopWatchdog();
        }
        catch { }

        try
        {
            var activity = Platform.CurrentActivity;
            if (activity != null)
            {
                activity.RunOnUiThread(() =>
                {
                    try
                    {
                        activity.FinishAffinity();
                        Java.Lang.JavaSystem.Exit(0);
                    }
                    catch
                    {
                        Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
                    }
                });
            }
            else
            {
                Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
            }
        }
        catch
        {
            Environment.Exit(1);
        }
    }
}
