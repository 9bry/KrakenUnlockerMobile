using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KrakenMobile.Services;

namespace KrakenMobile.Views;

public partial class HomePage : ContentPage
{
    private readonly HomePageViewModel _viewModel;

    public HomePage()
    {
        InitializeComponent();
        _viewModel = new HomePageViewModel();
        BindingContext = _viewModel;
        VersionValueLabel.Text = $"v{Services.UpdateService.GetCurrentVersion()}";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadProfileCommand.Execute(null);
    }
}

public partial class HomePageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _gamerTag = "Not Logged In";

    [ObservableProperty]
    private string _xuid = "N/A";

    [ObservableProperty]
    private string _xuidDisplay = "XUID: Not available";

    [ObservableProperty]
    private string _gamerPic = string.Empty;

    [ObservableProperty]
    private int _gamerScore;

    [ObservableProperty]
    private string _loginStatus = "Offline";

    [ObservableProperty]
    private string _statusText = "Welcome to Kraken Unlocker Mobile. Sign in to get started.";

    [ObservableProperty]
    private string _gameCount = "0 games";

    [RelayCommand]
    private async Task LoadProfileAsync()
    {
        if (!XboxAuthService.IsLoggedIn)
        {
            GamerTag = "Not Logged In";
            Xuid = "N/A";
            XuidDisplay = "XUID: Not available";
            GamerPic = string.Empty;
            GamerScore = 0;
            GameCount = "0 games";
            LoginStatus = "Offline";
            StatusText = "Not signed in. Go to Settings to sign in.";
            return;
        }

        StatusText = "Loading profile...";
        LoginStatus = "Online";

        try
        {
            await XboxAuthService.EnsureXuidAsync();
            var api = new XboxApiService();
            var (gamertag, xuid, profilePic, gs, bio, tier) = await api.GetProfileAsync();
            GamerTag = gamertag;
            Xuid = string.IsNullOrEmpty(xuid) ? "N/A" : xuid;
            XuidDisplay = $"XUID: {Xuid}";
            GamerScore = gs;
            GamerPic = profilePic;
            StatusText = $"Profile loaded. {bio}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading profile: {ex.Message}";
        }
    }
}
