using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KrakenMobile.Services;

namespace KrakenMobile.Views;

public partial class AchievementsPage : ContentPage
{
    private readonly AchievementsPageViewModel _viewModel;

    public AchievementsPage()
    {
        InitializeComponent();
        _viewModel = new AchievementsPageViewModel();
        BindingContext = _viewModel;
    }
}

public partial class AchievementsPageViewModel : ObservableObject
{
    private readonly XboxApiService _api = new();

    [ObservableProperty]
    private ObservableCollection<Achievement> _achievements = new();

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string _gameImage = string.Empty;

    [ObservableProperty]
    private string _gameInfo = "Select a game to view achievements";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isUnlockAllEnabled;

    [ObservableProperty]
    private string _selectedTitleId = string.Empty;

    [ObservableProperty]
    private string _selectedServiceConfigId = string.Empty;

    public void LoadAchievementsForGame(Game game)
    {
        GameName = game.Title;
        GameImage = game.Image;
        SelectedTitleId = game.TitleId;
        SelectedServiceConfigId = game.ServiceConfigId;
        GameInfo = $"{game.CurrentAchievements}/{game.TotalAchievements} Unlocked";
        _ = LoadAchievementsAsync();
    }

    [RelayCommand]
    private async Task LoadAchievementsAsync()
    {
        if (string.IsNullOrEmpty(SelectedTitleId)) return;

        IsLoading = true;
        Achievements.Clear();

        try
        {
            _api.RefreshClient();
            var achievements = await _api.GetAchievementsAsync(XboxAuthService.Xuid ?? "", SelectedTitleId);
            foreach (var achievement in achievements)
                Achievements.Add(achievement);

            var unlocked = achievements.Count(a => a.ProgressState == "Achieved");
            GameInfo = $"{unlocked}/{achievements.Count} Unlocked";
            IsUnlockAllEnabled = achievements.Any(a => a.ProgressState != "Achieved");
        }
        catch (Exception ex)
        {
            GameInfo = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UnlockAchievementAsync(Achievement? achievement)
    {
        if (achievement == null) return;

        if (!await AssertPremiumAsync("unlock", SelectedTitleId))
            return;

        var result = await _api.UnlockTitleBasedAchievementAsync(
            SelectedServiceConfigId, SelectedTitleId, XboxAuthService.Xuid ?? "", achievement.Id);
        if (result)
        {
            achievement.ProgressState = "Achieved";
            var unlocked = Achievements.Count(a => a.ProgressState == "Achieved");
            GameInfo = $"{unlocked}/{Achievements.Count} Unlocked";
        }
    }

    [RelayCommand]
    private async Task UnlockAllAsync()
    {
        if (string.IsNullOrEmpty(SelectedTitleId)) return;

        if (!await AssertPremiumAsync("unlock-all", SelectedTitleId))
            return;

        var locked = Achievements.Where(a => a.ProgressState != "Achieved").ToList();
        foreach (var achievement in locked)
        {
            if (!await SecurityService.ValidatePremiumOpAsync("unlock", SelectedTitleId))
                break;

            await _api.UnlockTitleBasedAchievementAsync(
                SelectedServiceConfigId, SelectedTitleId, XboxAuthService.Xuid ?? "", achievement.Id);
            achievement.ProgressState = "Achieved";
            GameInfo = $"Unlocking... {Achievements.Count(a => a.ProgressState == "Achieved")}/{Achievements.Count}";
            await Task.Delay(1000);
        }

        GameInfo = $"{Achievements.Count(a => a.ProgressState == "Achieved")}/{Achievements.Count} Unlocked";
        IsUnlockAllEnabled = false;
    }

    private async Task<bool> AssertPremiumAsync(string opType, string titleId)
    {
        if (!LicenseService.IsPremium)
        {
            GameInfo = "Premium license required to unlock";
            await Shell.Current.DisplayAlert("Premium Required",
                "A valid Kraken Unlocker license is required to unlock achievements.", "OK");
            return false;
        }

        var ok = await SecurityService.ValidatePremiumOpAsync(opType, titleId);
        if (!ok)
        {
            GameInfo = "License check failed";
            await Shell.Current.DisplayAlert("License Check Failed",
                "Could not verify your license with the server. Unlock blocked.", "OK");
        }

        return ok;
    }
}

public class AchievementStateColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var state = value?.ToString();
        return state switch
        {
            "Unlocked" or "Achieved" => "#00AA00",
            "Locked" => "#888888",
            _ => "#888888"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class UnlockButtonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isUnlockable)
            return isUnlockable ? "\U0001F513" : "\U0001F512";
        return "\U0001F512";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class UnlockBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var state = value?.ToString();
        return state switch
        {
            "Unlocked" or "Achieved" => "#1A1A1A",
            "Locked" => "#CC0000",
            _ => "#CC0000"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
