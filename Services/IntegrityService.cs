using System.Security.Cryptography;
using System.Diagnostics;
using KrakenMobile.Models;
using Newtonsoft.Json;

namespace KrakenMobile.Services;

public static class IntegrityService
{
    private const string GitHubLatestUrl = "https://api.github.com/repos/9bry/KrakenUnlocker/releases/latest";
    private const string HashAssetName = "KrakenMobile.apk.sha256";

    private static readonly HttpClient Http = new();

    static IntegrityService()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "KrakenMobile-Integrity/1.0");
        Http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        Http.Timeout = TimeSpan.FromSeconds(15);
    }

    public static async Task<IntegrityResult> CheckAsync()
    {
        try
        {
            var localHash = ComputeLocalApkHash();
            if (string.IsNullOrEmpty(localHash))
                return IntegrityResult.Unverifiable;

            var remoteHash = await FetchRemoteHashAsync();
            if (string.IsNullOrEmpty(remoteHash))
                return IntegrityResult.Unverifiable;

            return string.Equals(localHash.Trim(), remoteHash.Trim(), StringComparison.OrdinalIgnoreCase)
                ? IntegrityResult.Ok
                : IntegrityResult.Tampered;
        }
        catch
        {
            return IntegrityResult.Unverifiable;
        }
    }

    private static string? ComputeLocalApkHash()
    {
        try
        {
            var context = Android.App.Application.Context;
            var appInfo = context.ApplicationInfo;
            if (appInfo == null) return null;

            var apkPath = appInfo.SourceDir;
            if (string.IsNullOrEmpty(apkPath) || !System.IO.File.Exists(apkPath))
                return null;

            using var sha = SHA256.Create();
            using var stream = System.IO.File.OpenRead(apkPath);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> FetchRemoteHashAsync()
    {
        try
        {
            var responseString = await Http.GetStringAsync(GitHubLatestUrl);
            var release = JsonConvert.DeserializeObject<GitHubRelease>(responseString);
            if (release?.Assets == null) return null;

            var hashAsset = release.Assets.FirstOrDefault(a =>
                a.Name != null && a.Name.Equals(HashAssetName, StringComparison.OrdinalIgnoreCase));

            if (hashAsset?.BrowserDownloadUrl == null) return null;

            var hashContent = await Http.GetStringAsync(hashAsset.BrowserDownloadUrl);
            return hashContent.Trim();
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string?> GetRemoteApkHashAsync()
    {
        return await FetchRemoteHashAsync();
    }
}

public enum IntegrityResult
{
    Ok,
    Tampered,
    Unverifiable
}
