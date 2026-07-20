using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KrakenMobile.Services;

namespace KrakenMobile.Views;

public partial class GamesPage : ContentPage
{
    private readonly GamesPageViewModel _viewModel;

    public GamesPage()
    {
        InitializeComponent();
        _viewModel = new GamesPageViewModel();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadGamesCommand.Execute(null);
    }
}

public partial class GamesPageViewModel : ObservableObject
{
    private readonly XboxApiService _api = new();

    [ObservableProperty]
    private ObservableCollection<Game> _games = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string _gameCountText = "Loading games...";

    [ObservableProperty]
    private Game? _selectedGame;

    [RelayCommand]
    private async Task LoadGamesAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        GameCountText = "Loading games...";

        try
        {
            await XboxAuthService.EnsureXuidAsync();
            _api.RefreshClient();
            var games = await _api.GetGamesListAsync();
            Games.Clear();
            foreach (var game in games)
                Games.Add(game);

            GameCountText = $"{Games.Count} games";
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            GameCountText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectGame(Game? game)
    {
        SelectedGame = game;
    }

    [RelayCommand]
    private async Task SearchGamesAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadGamesAsync();
            return;
        }

        var filtered = Games.Where(g =>
            g.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        Games.Clear();
        foreach (var game in filtered)
            Games.Add(game);

        GameCountText = $"{Games.Count} games found";
    }
}
