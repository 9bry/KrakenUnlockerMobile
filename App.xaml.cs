using System.Threading.Tasks;
using KrakenMobile.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace KrakenMobile;

public partial class App : Application
{
    private static bool _securityPassed;
    private static bool _updateChecked;
    private static TaskCompletionSource? _securityBlocker;

    public App()
    {
        InitializeComponent();
        ThemeService.Initialize();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            ShowFatal("Unhandled Exception", e.ExceptionObject?.ToString() ?? "Unknown");
        TaskScheduler.UnobservedTaskException += (s, e) =>
            ShowFatal("Unobserved Task Exception", e.Exception?.ToString() ?? "Unknown");
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
                ShowFatal("Protection", $"Tamper detected: {protection.GetSummary()}");
                return;
            }

            ProtectionService.LockSignature();
            ProtectionService.Initialize();

            var integrityResult = await IntegrityService.CheckAsync();
            if (integrityResult == IntegrityResult.Tampered)
            {
                ShowFatal("Integrity", "Integrity check reported Tampered.");
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
        catch (Exception ex)
        {
            ShowFatal("Startup Error", ex.ToString());
        }
    }

    public static void ShowFatal(string title, string? message)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var page = Application.Current?.Windows?.FirstOrDefault()?.Page
                               ?? Application.Current?.MainPage;
                    if (page != null)
                        await page.DisplayAlert(title, message ?? "", "OK");
                }
                catch { }
            });
        }
        catch { }

        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "crashlog.txt");
            File.WriteAllText(path, $"[{title}]\n{message}\n");
        }
        catch { }
    }
}
