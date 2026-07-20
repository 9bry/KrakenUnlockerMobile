using Microsoft.Maui.Controls;

namespace KrakenMobile.Services;

public static class ThemeService
{
    private const string PrefKey = "kraken_dark_mode";

    private static readonly Dictionary<string, (string Dark, string Light)> Palette = new()
    {
        ["KrakenBackground"] = ("#0A0A0A", "#F2F2F2"),
        ["KrakenBackgroundDark"] = ("#050505", "#E8E8E8"),
        ["KrakenCard"] = ("#141414", "#FFFFFF"),
        ["KrakenCardDark"] = ("#0D0D0D", "#F7F7F7"),
        ["KrakenCardHighlight"] = ("#1A1A1A", "#EAEAEA"),
        ["KrakenBorder"] = ("#222222", "#D2D2D2"),
        ["KrakenBorderRed"] = ("#3A1010", "#E6B3B3"),
        ["KrakenTextPrimary"] = ("#FFFFFF", "#1A1A1A"),
        ["KrakenTextSecondary"] = ("#888888", "#5A5A5A"),
        ["KrakenTextTertiary"] = ("#555555", "#8A8A8A"),
        ["KrakenStatBg"] = ("#1A0505", "#FBE9E9"),
        ["KrakenStatBorder"] = ("#440B0B", "#F0C0C0"),
        ["KrakenEditorBg"] = ("#0A0A0A", "#FFFFFF"),
        ["KrakenSearchBg"] = ("#141414", "#FFFFFF"),
        ["KrakenDisabledText"] = ("#555555", "#AAAAAA"),
        ["KrakenAccent"] = ("#CC0000", "#CC0000"),
        ["KrakenAccentLight"] = ("#FF0033", "#FF0033")
    };

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
        try
        {
            var resources = Application.Current?.Resources;
            if (resources == null)
                return;

            foreach (var kv in Palette)
            {
                var hex = dark ? kv.Value.Dark : kv.Value.Light;
                resources[kv.Key] = Color.FromArgb(hex);
            }

            Application.Current!.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            // Never let a theme failure crash the app.
        }
    }

    public static void Initialize() => Apply(IsDarkMode);
}
