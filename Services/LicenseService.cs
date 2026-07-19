using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KrakenMobile.Services;

public static class LicenseService
{
    private static readonly byte[] _EdgePath = new byte[] { 0x21, 0xE1, 0xA6, 0x24, 0xD2, 0x1B, 0x4B, 0x40, 0x34, 0xCC, 0xF0, 0x35, 0x10, 0x0B, 0x8F, 0x50, 0xA5, 0xDF, 0x89, 0x7A, 0xEF, 0x9F, 0x50, 0x72, 0xEC, 0x3E, 0x8A, 0xE0, 0x26, 0x6D, 0x08, 0xFA, 0xF3, 0xE2, 0x32, 0xBF, 0xE6, 0xB5, 0xAE, 0x43, 0x71, 0xC3, 0x6F, 0x2A, 0xB9, 0x79, 0x76, 0x29 };
    private static string? _edgePath;
    private static readonly string EdgeUrl = Secrets.SupabaseUrl + (_edgePath ??= StringCryptor.Decode(_EdgePath));
    private static readonly string AnonKey = Secrets.SupabaseAnonKey;
    private static readonly HttpClient Http = new();
    public static string? LastRestoreError { get; private set; }
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KrakenXboxUnlocker", "license_debug.log");

    private static void Log(string msg)
    {
        try
        {
            var d = Path.GetDirectoryName(_logPath)!;
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
        }
        catch { }
    }

    public static event Action? StateChanged;

    private static string? _currentEmail;
    public static string? CurrentEmail
    {
        get => _currentEmail;
        private set { _currentEmail = value; StateChanged?.Invoke(); }
    }

    private static bool _isPremium;
    public static bool IsPremium
    {
        get => _isPremium;
        private set { _isPremium = value; StateChanged?.Invoke(); }
    }

    private static DateTime? _expiresAt;
    public static DateTime? ExpiresAt
    {
        get => _expiresAt;
        set { _expiresAt = value; StateChanged?.Invoke(); }
    }

    private static bool _isLifetime;
    public static bool IsLifetime
    {
        get => _isLifetime;
        private set { _isLifetime = value; StateChanged?.Invoke(); }
    }

    private static int _daysLeft;
    public static int DaysLeft
    {
        get => _daysLeft;
        private set { _daysLeft = value; StateChanged?.Invoke(); }
    }

    private static string _expiryDisplay = "";
    public static string ExpiryDisplay
    {
        get => _expiryDisplay;
        private set { _expiryDisplay = value; StateChanged?.Invoke(); }
    }

    private static DateTime? _tokenValidUntil;
    public static DateTime? TokenValidUntil
    {
        get => _tokenValidUntil;
        private set { _tokenValidUntil = value; }
    }

    static LicenseService()
    {
        Http.DefaultRequestHeaders.Add("apikey", AnonKey);
        Http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AnonKey);
    }

    private static string? _cachedMachineId;
    public static string GetMachineId()
    {
        if (_cachedMachineId != null) return _cachedMachineId;
        try
        {
            var androidId = Android.Provider.Settings.Secure.GetString(
                Android.App.Application.Context.ContentResolver,
                Android.Provider.Settings.Secure.AndroidId);
            if (!string.IsNullOrEmpty(androidId))
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(androidId));
                _cachedMachineId = Convert.ToHexString(hash)[..16].ToLower();
                return _cachedMachineId;
            }
        }
        catch { }

        var fallbackHash = SHA256.HashData(Encoding.UTF8.GetBytes("kraken_android_fallback"));
        _cachedMachineId = Convert.ToHexString(fallbackHash)[..16].ToLower();
        return _cachedMachineId;
    }

    public static bool TryRestoreSession()
    {
        try
        {
            var token = SecurityService.GetStoredToken();
            if (string.IsNullOrEmpty(token)) return false;

            var (valid, email, machineId, expiresAt) = SecurityService.ValidateSessionToken(token);
            if (!valid || email == null || expiresAt == null) return false;

            if (expiresAt.Value < DateTime.UtcNow) return false;

            if (!string.Equals(machineId, GetMachineId(), StringComparison.OrdinalIgnoreCase))
                return false;

            CurrentEmail = email;
            IsPremium = true;
            TokenValidUntil = expiresAt.Value;

            if (expiresAt.Value >= DateTime.UtcNow.AddYears(10))
            {
                IsLifetime = true;
                DaysLeft = 99999;
                ExpiryDisplay = "Lifetime";
                ExpiresAt = null;
            }
            else
            {
                IsLifetime = false;
                ExpiresAt = expiresAt.Value;
                DaysLeft = Math.Max(0, (int)(expiresAt.Value - DateTime.UtcNow).TotalDays);
                ExpiryDisplay = DaysLeft == 0 ? "Expires today" : $"{DaysLeft} days remaining";
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<(bool success, string error)> SendCodeAsync(string email)
    {
        if (!SecurityService.CheckRateLimit($"send-code:{email}", maxPerMinute: 3))
            return (false, "Too many requests. Wait a minute before trying again.");

        try
        {
            var url = $"{EdgeUrl}?action=send-code&email={Uri.EscapeDataString(email)}";
            Log($"SendCode URL: {url}");
            var resp = await Http.PostAsync(url, null);
            var body = await resp.Content.ReadAsStringAsync();
            Log($"SendCode Response: {resp.StatusCode} | {body}");
            var json = JObject.Parse(body);
            if (!resp.IsSuccessStatusCode)
                return (false, json["error"]?.ToString() ?? "Failed to send code.");
            if (json["sent"]?.ToObject<bool>() != true)
                return (false, json["error"]?.ToString() ?? "Failed to send code.");
            return (true, "");
        }
        catch (Exception ex)
        {
            Log($"SendCode EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return (false, ex.Message);
        }
    }

    public static async Task<(bool success, string error)> VerifyCodeAsync(string email, string code)
    {
        Log($"VerifyCode called: email={email} code={code}");

        if (!SecurityService.CheckRateLimit($"verify:{email}", maxPerMinute: 5))
        {
            Log("VerifyCode: rate limited");
            return (false, "Too many attempts. Wait a minute before trying again.");
        }

        try
        {
            var machineId = GetMachineId();
            var url = $"{EdgeUrl}?action=verify-code&email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(code)}&machine_id={machineId}";
            Log($"VerifyCode URL: {url}");
            var resp = await Http.PostAsync(url, null);
            var body = await resp.Content.ReadAsStringAsync();
            Log($"VerifyCode Response: {resp.StatusCode} | {body}");
            var json = JObject.Parse(body);
            if (!resp.IsSuccessStatusCode)
                return (false, json["error"]?.ToString() ?? "Invalid or expired code.");

            var expiresAtStr = json["expires_at"]?.ToString();

            DateTime tokenExpiry;
            bool isLifetime;
            if (string.IsNullOrEmpty(expiresAtStr) || expiresAtStr == "null")
            {
                isLifetime = true;
                tokenExpiry = DateTime.UtcNow.AddYears(100);
            }
            else
            {
                isLifetime = false;
                var licenseExpiry = DateTime.Parse(expiresAtStr, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                var fifteenHours = DateTime.UtcNow.AddHours(15);
                tokenExpiry = fifteenHours < licenseExpiry ? fifteenHours : licenseExpiry;
            }

            var token = SecurityService.CreateSessionToken(email, machineId, tokenExpiry);
            SecurityService.StoreToken(token);

            SetLicenseDetails(email, expiresAtStr, tokenExpiry);
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static void SetLicenseDetails(string email, string? expiresAtStr, DateTime tokenExpiry)
    {
        CurrentEmail = email;
        IsPremium = true;
        TokenValidUntil = tokenExpiry;

        if (string.IsNullOrEmpty(expiresAtStr) || expiresAtStr == "null")
        {
            IsLifetime = true;
            DaysLeft = 99999;
            ExpiryDisplay = "Lifetime";
            ExpiresAt = null;
        }
        else
        {
            IsLifetime = false;
            var licenseExpiry = DateTime.Parse(expiresAtStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            DaysLeft = Math.Max(0, (int)(licenseExpiry - DateTime.UtcNow).TotalDays);
            ExpiryDisplay = DaysLeft == 0 ? "Expires today" : $"{DaysLeft} days remaining";
            ExpiresAt = licenseExpiry;
        }
    }

    public static void Logout()
    {
        CurrentEmail = null;
        IsPremium = false;
        ExpiresAt = null;
        TokenValidUntil = null;
        SecurityService.ClearStoredToken();
    }
}
