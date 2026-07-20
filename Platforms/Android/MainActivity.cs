using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace KrakenMobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public static Activity? CurrentActivity;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
        {
            var msg = e.Exception?.ToString() ?? "Unknown";

            try
            {
                var dir = Android.App.Application.Context?.GetExternalFilesDir(null);
                if (dir != null)
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(dir.AbsolutePath, "crashlog.txt"),
                        "[Native UnhandledException]\n" + msg + "\n");
            }
            catch { }

            try
            {
                RunOnUiThread(() =>
                {
                    try
                    {
                        var builder = new AlertDialog.Builder(this);
                        builder.SetTitle("Kraken Crash");
                        var shown = msg.Length > 3000 ? msg.Substring(0, 3000) : msg;
                        builder.SetMessage(shown);
                        builder.SetPositiveButton("OK", (sender, args) => { });
                        builder.Create().Show();
                    }
                    catch { }
                });
            }
            catch { }

            e.Handled = true;
        };

        base.OnCreate(savedInstanceState);
        CurrentActivity = this;
    }

    protected override void OnResume()
    {
        base.OnResume();
        CurrentActivity = this;
    }
}
