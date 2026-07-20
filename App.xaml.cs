using System.Threading.Tasks;
using KrakenMobile.Services;
using KrakenMobile.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace KrakenMobile;

public partial class App : Application
{
    private static bool _securityPassed;
    private static bool _updateChecked;
    private static TaskCompletionSource? _securityBlocker;
    private static Exception? _bootError;

    public App()
    {
        Exception? bootError = null;
        try
        {
            InitializeComponent();
            ThemeService.Initialize();
        }
        catch (Exception ex)
        {
            bootError = ex;
        }

        _bootError = bootError;

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            ShowFatal("Unhandled Exception", e.ExceptionObject?.ToString() ?? "Unknown");
        TaskScheduler.UnobservedTaskException += (s, e) =>
            ShowFatal("Unobserved Task Exception", e.Exception?.ToString() ?? "Unknown");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (_bootError != null)
            return new Window(new ErrorPage("App boot failed", _bootError.ToString()));

        Window window;
        try
        {
            window = new Window(new AppShell());
        }
        catch (Exception ex)
        {
            return new Window(new ErrorPage("Shell creation failed", ex.ToString()));
        }

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
            var protection = await SafeStep("Protection", () => Task.FromResult(ProtectionService.Check()));
            if (protection == null) return;
            if (protection.IsTampered)
            {
                FailToErrorPage(window, "Protection", $"Tamper detected: {protection.GetSummary()}");
                return;
            }

            await SafeStep("Protection.LockSignature", () =>
            {
                ProtectionService.LockSignature();
                ProtectionService.Initialize();
                return Task.CompletedTask;
            });

            var integrityResult = await SafeStep("Integrity", () => IntegrityService.CheckAsync());
            if (integrityResult == null) return;
            if (integrityResult == IntegrityResult.Tampered)
            {
                FailToErrorPage(window, "Integrity", "Integrity check reported Tampered.");
                return;
            }

            _securityPassed = true;

            var updateResult = await SafeStep("Update", () => UpdateService.CheckForUpdateAsync());
            if (updateResult == null) return;
            if (updateResult.Severity == UpdateSeverity.Hard)
            {
                _updateChecked = true;
                var forcePage = new ForceUpdatePage(
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
            FailToErrorPage(window, "Startup Error", ex.ToString());
        }
    }

    private static async Task<T?> SafeStep<T>(string label, Func<Task<T>> step)
    {
        try
        {
            return await step();
        }
        catch (Exception ex)
        {
            FailToErrorPage(Application.Current?.Windows?.FirstOrDefault(), $"Startup Fault: {label}", ex.ToString());
            return default;
        }
    }

    private static async Task SafeStep(string label, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception ex)
        {
            FailToErrorPage(Application.Current?.Windows?.FirstOrDefault(), $"Startup Fault: {label}", ex.ToString());
        }
    }

    public static void ShowFatal(string title, string? message)
    {
        var window = Application.Current?.Windows?.FirstOrDefault();
        FailToErrorPage(window, title, message);

        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var page = window?.Page;
                    if (page != null)
                        await page.DisplayAlert(title, message ?? "", "OK");
                }
                catch { }
            });
        }
        catch { }
    }

    private static void FailToErrorPage(Window? window, string title, string? message)
    {
        try
        {
            WriteCrashToExternal($"[{title}]\n{message}\n");
        }
        catch { }

        try
        {
            if (window != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        window.Page = new ErrorPage(title, message ?? "");
                    }
                    catch { }
                });
            }
        }
        catch { }
    }

    public static void ShowErrorOnScreen(Exception? ex)
    {
        var msg = ex?.ToString() ?? "Unknown";
        WriteCrashToExternal("[Native UnhandledException]\n" + msg);

        try
        {
            var window = Application.Current?.Windows?.FirstOrDefault();
            if (window != null)
                window.Page = new ErrorPage("Native Crash", msg);
        }
        catch { }
    }

    private static void WriteCrashToExternal(string text)
    {
#if ANDROID
        var ctx = Android.App.Application.Context;
        var dir = ctx?.GetExternalFilesDir(null);
        if (dir != null)
        {
            var ext = Path.Combine(dir.AbsolutePath, "crashlog.txt");
            File.WriteAllText(ext, text);
        }

        try
        {
            var dl = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
            if (dl != null)
                File.WriteAllText(System.IO.Path.Combine(dl.AbsolutePath, "kraken_crashlog.txt"), text);
        }
        catch { }
#endif
    }
}
