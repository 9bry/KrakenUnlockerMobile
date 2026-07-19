using System.Diagnostics;
using Newtonsoft.Json;
using KrakenMobile.Models;

namespace KrakenMobile.Services;

public static class UpdateService
{
    private const string CurrentVersion = "1.0";
    private const string AllReleasesUrl = "https://api.github.com/repos/9bry/KrakenUnlockerMobile/releases";

    private static readonly HttpClient Http = new();
    private static readonly string _logPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KrakenUnlockerMobile", "update.log");

    static UpdateService()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "KrakenUnlockerMobile/1.0");
        Http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        Http.Timeout = TimeSpan.FromSeconds(15);
    }

    public static string GetCurrentVersion() => CurrentVersion;

    public static async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            Log("Checking for updates...");
            var responseString = await Http.GetStringAsync(AllReleasesUrl);
            var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(responseString);
            if (releases == null || releases.Count == 0)
            {
                Log("No releases found or null response");
                return new UpdateCheckResult { Severity = UpdateSeverity.None };
            }

            var currentVersion = new Version(CurrentVersion);

            GitHubRelease? bestRelease = null;
            Version? bestVersion = null;

            foreach (var release in releases)
            {
                if (release.Draft || release.Prerelease) continue;
                if (string.IsNullOrEmpty(release.TagName)) continue;

                var tagVersion = release.TagName.TrimStart('v', 'V');
                if (!Version.TryParse(tagVersion, out var parsed)) continue;

                if (parsed.Major == currentVersion.Major && parsed.Minor == currentVersion.Minor)
                    continue;

                if (bestVersion == null || parsed > bestVersion)
                {
                    bestVersion = parsed;
                    bestRelease = release;
                }
            }

            if (bestRelease == null || bestVersion == null)
            {
                Log($"Up to date. Current: {CurrentVersion}");
                return new UpdateCheckResult { Severity = UpdateSeverity.None };
            }

            var majorDiff = bestVersion.Major - currentVersion.Major;
            var minorDiff = bestVersion.Minor - currentVersion.Minor;
            var versionsBehind = (majorDiff * 100) + minorDiff;

            Log($"Update available: {bestRelease.TagName} (behind by {versionsBehind} minor versions)");

            var apkAsset = bestRelease.Assets?.FirstOrDefault(a =>
                a.Name != null && a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));

            return new UpdateCheckResult
            {
                Severity = UpdateSeverity.Hard,
                LatestVersion = bestVersion.ToString(),
                CurrentVersion = CurrentVersion,
                ReleaseNotes = bestRelease.Body ?? "",
                DownloadUrl = apkAsset?.BrowserDownloadUrl ?? bestRelease.HtmlUrl ?? "",
                ReleaseUrl = bestRelease.HtmlUrl ?? "",
                VersionsBehind = versionsBehind
            };
        }
        catch (Exception ex)
        {
            Log($"Update check failed: {ex.Message}");
            return new UpdateCheckResult { Severity = UpdateSeverity.None };
        }
    }

    private static void Log(string msg)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_logPath)!;
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
        }
        catch { }
    }
}

public enum UpdateSeverity
{
    None,
    Soft,
    Hard
}

public class UpdateCheckResult
{
    public UpdateSeverity Severity { get; set; }
    public string LatestVersion { get; set; } = "";
    public string CurrentVersion { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ReleaseUrl { get; set; } = "";
    public int VersionsBehind { get; set; }
}
