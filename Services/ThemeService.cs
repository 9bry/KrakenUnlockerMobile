using Microsoft.Maui.Controls;

namespace KrakenMobile.Services;

public static class ThemeService
{
    private static readonly Uri DarkUri = new("Resources/Styles/Themes/DarkTheme.xaml", UriKind.Relative);
    private static readonly Uri LightUri = new("Resources/Styles/Themes/LightTheme.xaml", UriKind.Relative);
    private const string PrefKey = "kraken_dark_mode";

    public static bool IsDarkMode
    {
        get => Preferences.Default.Get(PrefKey, true);
        set
        {
            Preferences.Default.Set(PrefKey, value);
            Apply(value);
        }
    }

    public static void Apply(bool dark)
    {
        if (Application.Current?.Resources == null)
            return;

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            Uri.Compare(d.Source, DarkUri, UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0 ||
            Uri.Compare(d.Source, LightUri, UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0);

        if (existing != null)
            merged.Remove(existing);

        merged.Add(new ResourceDictionary { Source = dark ? DarkUri : LightUri });
        Application.Current.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
    }

    public static void Initialize()
    {
        Apply(IsDarkMode);
    }
}
