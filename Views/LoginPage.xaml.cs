using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KrakenMobile.Services;

namespace KrakenMobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginPageViewModel _viewModel;

    public LoginPage()
    {
        InitializeComponent();
        _viewModel = new LoginPageViewModel();
        BindingContext = _viewModel;
        VersionLabel.Text = $"v{Services.UpdateService.GetCurrentVersion()}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (XboxAuthService.IsLoggedIn)
        {
            await Shell.Current.GoToAsync("//HomePage");
        }
    }
}

public partial class LoginPageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoggingIn;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [RelayCommand]
    private async Task LoginWithXboxAsync()
    {
        if (IsLoggingIn) return;

        IsLoggingIn = true;
        StatusText = "Connecting to Xbox Live...";

        var result = await XboxAuthService.LoginAsync();

        if (result)
        {
            StatusText = "Login successful! Redirecting...";
            await Task.Delay(500);
            await Shell.Current.GoToAsync("//HomePage");
        }
        else
        {
            StatusText = XboxAuthService.LastError ?? "Login failed.";
        }

        IsLoggingIn = false;
    }
}
