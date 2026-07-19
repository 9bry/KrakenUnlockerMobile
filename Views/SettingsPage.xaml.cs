using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KrakenMobile.Services;

namespace KrakenMobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsPageViewModel _viewModel;

    public SettingsPage()
    {
        InitializeComponent();
        _viewModel = new SettingsPageViewModel();
        BindingContext = _viewModel;
    }
}

public partial class SettingsPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _versionText = $"v{UpdateService.GetCurrentVersion()}";

    [ObservableProperty]
    private string _licenseStatus = "Active";

    [ObservableProperty]
    private bool _darkModeEnabled = true;

    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private string _signInButtonText = "Sign in with Xbox";

    [ObservableProperty]
    private string _accountStatus = "Not signed in";

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _eventTokenText = XboxAuthService.ManualEventsToken ?? "";

    [ObservableProperty]
    private string _eventTokenStatus = XboxAuthService.HasManualEventsToken
        ? "Custom event token active"
        : "Using auto-generated token";

    [ObservableProperty]
    private string _xblTokenText = XboxAuthService.ManualXblToken ?? "";

    [ObservableProperty]
    private string _xblTokenStatus = XboxAuthService.HasManualXblToken
        ? "Manual XAuth active" + (string.IsNullOrEmpty(XboxAuthService.ManualXuid) ? "" : $" (XUID: {XboxAuthService.ManualXuid})")
        : "Using Microsoft login";

    partial void OnDarkModeEnabledChanged(bool value)
    {
        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
        }
    }

    partial void OnIsLoggedInChanged(bool value)
    {
        SignInButtonText = value ? "Sign Out" : "Sign in with Xbox";
        AccountStatus = value ? "Signed in to Xbox Live" : "Not signed in";
    }

    public SettingsPageViewModel()
    {
        IsLoggedIn = XboxAuthService.IsLoggedIn;
        UpdateLoginState();
    }

    private void UpdateLoginState()
    {
        IsLoggedIn = XboxAuthService.IsLoggedIn;
        SignInButtonText = IsLoggedIn ? "Sign Out" : "Sign in with Xbox";
        AccountStatus = IsLoggedIn
            ? $"Signed in (XUID: {XboxAuthService.Xuid})"
            : "Not signed in";
    }

    [RelayCommand]
    private async Task SignInOutAsync()
    {
        if (IsLoggedIn)
        {
            var confirm = await Shell.Current.DisplayAlert("Sign Out", "Are you sure you want to sign out?", "Yes", "Cancel");
            if (!confirm) return;

            await XboxAuthService.LogoutAsync();
            UpdateLoginState();
        }
        else
        {
            AccountStatus = "Connecting to Xbox Live...";
            var result = await XboxAuthService.LoginAsync();

            if (result)
            {
                UpdateLoginState();
            }
            else
            {
                AccountStatus = XboxAuthService.LastError ?? "Login failed";
            }
        }
    }

    [RelayCommand]
    private async Task AboutAsync()
    {
        await Shell.Current.DisplayAlert(
            "Kraken Unlocker",
            $"Version {UpdateService.GetCurrentVersion()}\n\nXbox Achievement Unlocker\nPorted to mobile with .NET MAUI.\n\nKraken Xbox Unlocker Team",
            "Close");
    }

    [RelayCommand]
    private void SaveEventToken()
    {
        var token = EventTokenText?.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            EventTokenStatus = "Enter a token before saving";
            return;
        }

        if (!token.StartsWith("x:", StringComparison.OrdinalIgnoreCase) &&
            !token.Contains("XBL3.0"))
        {
            EventTokenStatus = "Invalid token format (must be x:XBL3.0 ...)";
            return;
        }

        XboxAuthService.ManualEventsToken = token;
        EventTokenStatus = "Custom event token saved";
    }

    [RelayCommand]
    private void ClearEventToken()
    {
        EventTokenText = "";
        XboxAuthService.ManualEventsToken = "";
        EventTokenStatus = "Using auto-generated token";
    }

    [RelayCommand]
    private void SaveXblToken()
    {
        var token = XblTokenText?.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            XblTokenStatus = "Enter a token before saving";
            return;
        }

        if (!token.Contains("XBL3.0") && !token.StartsWith("x:", StringComparison.OrdinalIgnoreCase))
        {
            XblTokenStatus = "Invalid token format (must be XBL3.0 x=...;...)";
            return;
        }

        XboxAuthService.ManualXblToken = token;

        var xuid = XboxAuthService.ManualXuid;
        XblTokenStatus = string.IsNullOrEmpty(xuid)
            ? "Manual XAuth saved (XUID not detected in token)"
            : $"Manual XAuth saved (XUID: {xuid})";
    }

    [RelayCommand]
    private void ClearXblToken()
    {
        XblTokenText = "";
        XboxAuthService.ManualXblToken = "";
        XblTokenStatus = "Using Microsoft login";
    }
}
