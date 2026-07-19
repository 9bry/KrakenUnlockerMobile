using System.IO;
using System.Net.Http;
using Android.Content;
using Microsoft.Maui.Storage;

namespace KrakenMobile.Views;

public partial class ForceUpdatePage : ContentPage
{
    private readonly string _downloadUrl;
    private readonly HttpClient _http = new();

    public ForceUpdatePage(string latestVersion, string currentVersion, string releaseNotes, string downloadUrl)
    {
        InitializeComponent();
        _downloadUrl = downloadUrl;

        LatestVersionLabel.Text = $"v{latestVersion}";
        CurrentVersionLabel.Text = $"v{currentVersion}";

        if (!string.IsNullOrWhiteSpace(releaseNotes))
        {
            ReleaseNotesBorder.IsVisible = true;
            ReleaseNotesLabel.Text = releaseNotes;
        }

        DownloadButton.Clicked += OnDownloadClicked;
        ExitButton.Clicked += OnExitClicked;
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_downloadUrl))
        {
            await Browser.OpenAsync(_downloadUrl, BrowserLaunchMode.SystemPreferred);
            return;
        }

        DownloadButton.IsEnabled = false;
        DownloadButton.Text = "DOWNLOADING...";

        try
        {
            var dir = Path.Combine(FileSystem.CacheDirectory, "updates");
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, "KrakenUnlockerUpdate.apk");

            using (var fs = File.Create(filePath))
            {
                var response = await _http.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await response.Content.CopyToAsync(fs);
            }

            DownloadButton.Text = "INSTALLING...";
            InstallApk(filePath);
        }
        catch
        {
            try
            {
                await Browser.OpenAsync(_downloadUrl, BrowserLaunchMode.SystemPreferred);
            }
            catch { }

            await Task.Delay(1000);
            Application.Current?.Quit();
        }
    }

    private void InstallApk(string filePath)
    {
        var context = Android.App.Application.Context;
        var authority = "com.companyname.krakenmobile.fileprovider";
        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, authority, new Java.IO.File(filePath));

        var intent = new Intent(Intent.ActionInstallPackage);
        intent.SetDataAndType(uri, "application/vnd.android.package-archive");
        intent.AddFlags(ActivityFlags.NewTask);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        context.StartActivity(intent);
    }

    private async void OnExitClicked(object? sender, EventArgs e)
    {
        Application.Current?.Quit();
    }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }
}
