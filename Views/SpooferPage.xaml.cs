using System.Text.RegularExpressions;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KrakenMobile.Services;
using Microsoft.Maui.ApplicationModel;

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
    private Timer? _heartbeatTimer;
    private string? _spoofTitleId;

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
            GameTitleId = "Enter the Title ID below";
        }
    }

    [RelayCommand]
    private async Task ToggleSpoofAsync()
    {
        if (IsSpoofing)
        {
            StopSpoof();
            return;
        }

        var titleId = await ResolveTitleIdAsync();
        if (string.IsNullOrEmpty(titleId))
        {
            SpoofingStatus = "Spoofing Failed";
            HeartbeatStatus = "Enter a valid Title ID or game name";
            HeartbeatColor = "#CC0000";
            return;
        }

        _spoofTitleId = titleId;
        IsSpoofing = true;
        SpoofingStatus = "Spoofing Active";
        CurrentSpoofGame = $"{GameName} ({titleId})";
        SpoofingStatusIcon = "\u25CF";
        SpoofingBorderColor = "#CC0000";
        SpoofButtonText = "Stop Spoofing";
        HeartbeatStatus = "Sending heartbeat...";
        HeartbeatColor = "#00AA00";

        _heartbeatTimer = new Timer(_ => _ = SendBeat(), null, 0, 60_000);
    }

    [RelayCommand]
    private void StopSpoof()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        _ = new XboxApiService().StopSpoofHeartbeatAsync();
        ResetSpoofState();
    }

    private async Task SendBeat()
    {
        try
        {
            var ok = await new XboxApiService().SendSpoofHeartbeatAsync(_spoofTitleId ?? "");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HeartbeatStatus = ok ? $"Heartbeat OK ({DateTime.Now:T})" : "Heartbeat failed";
                HeartbeatColor = ok ? "#00AA00" : "#CC0000";
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HeartbeatStatus = "Heartbeat error";
                HeartbeatColor = "#CC0000";
            });
        }
    }

    private async Task<string?> ResolveTitleIdAsync()
    {
        var raw = GameSearchText?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (Regex.IsMatch(raw, @"^[0-9A-Fa-f]+$"))
            return raw;

        try
        {
            var api = new XboxApiService();
            await XboxAuthService.EnsureXuidAsync();
            api.RefreshClient();
            var games = await api.GetGamesListAsync();
            var match = games.FirstOrDefault(g => g.Title.Contains(raw, StringComparison.OrdinalIgnoreCase));
            return match?.TitleId;
        }
        catch
        {
            return null;
        }
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
