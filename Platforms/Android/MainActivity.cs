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
                App.ShowErrorOnScreen(e.Exception);
            }
            catch { }
            finally
            {
                e.Handled = true;
            }
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
