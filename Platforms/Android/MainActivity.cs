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
            try
            {
                var dir = Android.App.Application.Context?.GetExternalFilesDir(null);
                if (dir != null)
                {
                    var ext = System.IO.Path.Combine(dir.AbsolutePath, "crashlog.txt");
                    System.IO.File.WriteAllText(ext, "[Native UnhandledException]\n" + (e.Exception?.ToString() ?? "Unknown") + "\n");
                }

                try
                {
                    var dl = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
                    if (dl != null)
                        System.IO.File.WriteAllText(System.IO.Path.Combine(dl.AbsolutePath, "kraken_crashlog.txt"),
                            "[Native UnhandledException]\n" + (e.Exception?.ToString() ?? "Unknown") + "\n");
                }
                catch { }
            }
            catch { }
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
