using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KrakenMobile.Services;

namespace KrakenMobile.Views;

public partial class SpooferPage : ContentPage
{
    private readonly SpooferPageViewModel _viewModel;

    public SpooferPage()
    {
        InitializeComponent();
        _viewModel = new SpooferPageViewModel();
        BindingContext = _viewModel;
    }
}

public partial class SpooferPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _gameSearchText = string.Empty;

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string _gameTitleId = string.Empty;

    [ObservableProperty]
    private string _gameGamerscore = "0";

    [ObservableProperty]
    private string _gameType = "N/A";

    [ObservableProperty]
    private bool _hasGameInfo;

    [ObservableProperty]
    private bool _isSpoofing;

    [ObservableProperty]
    private string _spoofingStatus = "Not Spoofing";

    [ObservableProperty]
    private string _currentSpoofGame = "No active spoof";

    [ObservableProperty]
    private string _spoofingStatusIcon = "\u25CB";

    [ObservableProperty]
    private string _spoofingBorderColor = "#222222";

    [ObservableProperty]
    private string _spoofButtonText = "Start Spoofing";

    [ObservableProperty]
    private string _heartbeatStatus = "Waiting...";

    [ObservableProperty]
    private string _heartbeatColor = "#444444";

    [RelayCommand]
    private void SearchGame()
    {
        if (!string.IsNullOrWhiteSpace(GameSearchText))
        {
            HasGameInfo = true;
            GameName = GameSearchText;
            GameTitleId = "Searching...";
        }
    }

    [RelayCommand]
    private void ToggleSpoof()
    {
        IsSpoofing = !IsSpoofing;

        if (IsSpoofing)
        {
            SpoofingStatus = "Spoofing Active";
            CurrentSpoofGame = GameName;
            SpoofingStatusIcon = "\u25CF";
            SpoofingBorderColor = "#CC0000";
            SpoofButtonText = "Spoofing...";
            HeartbeatStatus = "Heartbeat active";
            HeartbeatColor = "#00AA00";
        }
        else
        {
            ResetSpoofState();
        }
    }

    [RelayCommand]
    private void StopSpoof()
    {
        ResetSpoofState();
    }

    private void ResetSpoofState()
    {
        IsSpoofing = false;
        SpoofingStatus = "Not Spoofing";
        CurrentSpoofGame = "No active spoof";
        SpoofingStatusIcon = "\u25CB";
        SpoofingBorderColor = "#222222";
        SpoofButtonText = "Start Spoofing";
        HeartbeatStatus = "Waiting...";
        HeartbeatColor = "#444444";
    }
}
