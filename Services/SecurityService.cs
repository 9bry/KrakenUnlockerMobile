using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace KrakenMobile.Services;

public static class SecurityService
{
    private static readonly byte[] _HmacKeyBytes = new byte[]
    {
        0x11, 0x29, 0x1C, 0x36, 0x6D, 0x31, 0x3F, 0x39, 0x00, 0x53, 0x1C, 0x3A,
        0x33, 0x09, 0x04, 0x59, 0x09, 0x00, 0x5F, 0x1F, 0x31, 0x5D, 0x40, 0x43,
        0x46, 0x2C, 0x27, 0x46, 0x15, 0x05, 0x4B, 0x0D, 0x25, 0x33, 0x31, 0x3C,
        0x3D, 0x20, 0xCB, 0xB2, 0xFB, 0xA2
    };
    private static byte[]? _hmacKey;
    private static byte[] HmacKeyBytes => _hmacKey ??= _HmacKeyBytes;
    private static DateTime? _sessionTokenIssued;
    private static readonly object _rateLock = new();
    private static readonly Dictionary<string, List<DateTime>> _operationTimestamps = new();

    public static string GenerateFingerprint()
    {
        var parts = new List<string>();

        try
        {
            var androidId = Android.Provider.Settings.Secure.GetString(
                Android.App.Application.Context.ContentResolver,
                Android.Provider.Settings.Secure.AndroidId);
            if (!string.IsNullOrEmpty(androidId)) parts.Add(androidId);
        }
        catch { }

        try
        {
            parts.Add(Android.OS.Build.Manufacturer ?? "");
            parts.Add(Android.OS.Build.Model ?? "");
            parts.Add(Android.OS.Build.Device ?? "");
            parts.Add(Android.OS.Build.Serial ?? "");
        }
        catch { }

        try
        {
            var raw = string.Join("|", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            if (string.IsNullOrEmpty(raw))
                raw = Android.OS.Build.Manufacturer + Android.OS.Build.Model + Android.OS.Build.Device;

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLower();
        }
        catch
        {
            return "unknown_fingerprint";
        }
    }

    public static string CreateSessionToken(string email, string machineId, DateTime expiresAt)
    {
        var expiryTicks = expiresAt.Ticks;
        var payload = $"{email.Length}:{email}:{machineId}:{expiryTicks}";
        var hmac = ComputeHmac(payload);
        var tokenData = $"{payload}:{hmac}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
    }

    public static (bool valid, string? email, string? machineId, DateTime? expiresAt) ValidateSessionToken(string token)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(':');
            if (parts.Length != 5) return (false, null, null, null);

            var emailLen = int.Parse(parts[0]);
            var email = parts[1];
            var machineId = parts[2];
            var expiryTicks = long.Parse(parts[3]);
            var receivedHmac = parts[4];

            if (email.Length != emailLen) return (false, null, null, null);

            var payload = $"{emailLen}:{email}:{machineId}:{expiryTicks}";
            var expectedHmac = ComputeHmac(payload);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(receivedHmac),
                    Encoding.UTF8.GetBytes(expectedHmac)))
                return (false, null, null, null);

            var expiresAt = new DateTime(expiryTicks, DateTimeKind.Utc);

            if (expiresAt < DateTime.UtcNow)
                return (false, null, null, null);

            var currentMachineId = LicenseService.GetMachineId();
            if (!string.Equals(machineId, currentMachineId, StringComparison.OrdinalIgnoreCase))
                return (false, null, null, null);

            return (true, email, machineId, expiresAt);
        }
        catch
        {
            return (false, null, null, null);
        }
    }

    private static string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(HmacKeyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLower();
    }

    public static bool DetectHooks()
    {
        // No Win32 API hooks on Android
        try
        {
            if (System.Diagnostics.Debugger.IsAttached) return true;
        }
        catch { }
        return false;
    }

    public static bool CheckRateLimit(string operationType, int maxPerMinute = 10)
    {
        lock (_rateLock)
        {
            var now = DateTime.UtcNow;
            if (!_operationTimestamps.ContainsKey(operationType))
                _operationTimestamps[operationType] = new List<DateTime>();

            _operationTimestamps[operationType].RemoveAll(t => (now - t).TotalSeconds > 60);

            if (_operationTimestamps[operationType].Count >= maxPerMinute)
                return false;

            _operationTimestamps[operationType].Add(now);
            return true;
        }
    }

    public static bool ValidateSession()
    {
        try
        {
            if (!LicenseService.IsPremium) return true;
            var token = GetStoredToken();
            if (string.IsNullOrEmpty(token)) return false;
            var (valid, _, _, _) = ValidateSessionToken(token);
            return valid;
        }
        catch
        {
            return false;
        }
    }

    public static void StoreToken(string token)
    {
        try
        {
            var path = GetTokenPath();
            var encoded = XorEncode(token, GetCurrentXorKey());
            File.WriteAllText(path, encoded);
            _sessionTokenIssued = DateTime.UtcNow;
        }
        catch { }
    }

    public static string? GetStoredToken()
    {
        try
        {
            var path = GetTokenPath();
            if (!File.Exists(path)) return null;
            var encoded = File.ReadAllText(path);
            return XorEncode(encoded, GetCurrentXorKey());
        }
        catch { return null; }
    }

    public static void ClearStoredToken()
    {
        try
        {
            var path = GetTokenPath();
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    private static string GetTokenPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KrakenXboxUnlocker");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, ".session");
    }

    private static string GetCurrentXorKey()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        return "KxU_" + today + "_3nd";
    }

    private static string XorEncode(string input, string key)
    {
        var result = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
            result[i] = (char)(input[i] ^ key[i % key.Length]);
        return new string(result);
    }

    private static readonly HttpClient _opHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly Dictionary<string, (string token, DateTime expiry)> _tokenCache = new();
    private static readonly object _tokenLock = new();

    public static async Task<bool> ValidatePremiumOpAsync(string opType, string titleId = "")
    {
        if (!LicenseService.IsPremium || string.IsNullOrEmpty(LicenseService.CurrentEmail))
            return false;

        var cacheKey = $"{opType}:{titleId}";
        lock (_tokenLock)
        {
            if (_tokenCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.expiry)
                return true;
        }

        try
        {
            var email = LicenseService.CurrentEmail;
            var machineId = LicenseService.GetMachineId();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            var url = $"{Secrets.SupabaseUrl}/functions/v1/kraken-auth?action=validate-unlock" +
                      $"&email={Uri.EscapeDataString(email)}" +
                      $"&machine_id={Uri.EscapeDataString(machineId)}" +
                      $"&op={Uri.EscapeDataString(opType)}" +
                      $"&title_id={Uri.EscapeDataString(titleId)}" +
                      $"&ts={timestamp}";

            _opHttp.DefaultRequestHeaders.Clear();
            _opHttp.DefaultRequestHeaders.Add("apikey", Secrets.SupabaseAnonKey);

            var resp = await _opHttp.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            var json = JObject.Parse(body);

            if (json["valid"]?.ToObject<bool>() == true)
            {
                var expires = json["expires"]?.ToObject<long>() ?? 0;
                var expiry = DateTimeOffset.FromUnixTimeMilliseconds(expires).UtcDateTime;
                var tokenVal = json["token"]?.ToString() ?? "";

                lock (_tokenLock)
                    _tokenCache[cacheKey] = (tokenVal, expiry);

                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
