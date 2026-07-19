using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Maui.Storage;

namespace KrakenMobile.Services;

public static class XboxAuthService
{
    private const string ClientId = "000000004424da1f";
    private const string Scope = "service::user.auth.xboxlive.com::MBI_SSL";
    private const string DeviceUrl = "https://device.auth.xboxlive.com/device/authenticate";
    private const string DeviceRp = "http://auth.xboxlive.com";
    private const string TitleAuthUrl = "https://title.auth.xboxlive.com/title/authenticate";
    private const string UserUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string XboxRp = "http://xboxlive.com";
    private const string EventsRp = "http://events.xboxlive.com";
    private const string UserAgent = "Mozilla/5.0 (XboxReplay; XboxLiveAuth/3.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36";

    private static readonly HttpClient Http = new();
    private static readonly HttpClient SignedHttp = new();
    private static string? _cachedXblToken, _cachedEventsToken, _cachedXuid, _cachedUhs, _cachedSpoofToken;
    private static string? _manualEventsToken;
    private const string ManualEventsTokenKey = "kx_manual_events_token";
    private static DateTime _cachedAt;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(20);
    private static PopCryptoProvider? _pop;
    private static IPublicClientApplication? _msalApp;

    public static string? LastError;
    public static void Diag(string msg)
    {
        LastError = msg;
        System.Diagnostics.Debug.WriteLine($"[XboxAuth] {msg}");
    }

    public static bool IsLoggedIn => !string.IsNullOrEmpty(_cachedXblToken) && DateTime.UtcNow - _cachedAt < Ttl;
    public static string? Xuid => _cachedXuid;
    public static string? Uhs => _cachedUhs;

    static XboxAuthService()
    {
        Http.DefaultRequestHeaders.Add("x-xbl-contract-version", "2");
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static IPublicClientApplication GetMsalApp()
    {
        if (_msalApp != null) return _msalApp;

        var builder = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
            .WithRedirectUri("msauth://com.kraken.xboxunlocker/callback");

        _msalApp = builder.Build();
        return _msalApp;
    }

    public static async Task<List<string>> GetAccountsAsync()
    {
        try
        {
            var app = GetMsalApp();
            var accounts = await app.GetAccountsAsync();
            return accounts.Select(a => a.Username).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static async Task<bool> LoginAsync()
    {
        try
        {
            var app = GetMsalApp();
            var accounts = await app.GetAccountsAsync();
            AuthenticationResult? authResult = null;
            foreach (var account in accounts)
            {
                try
                {
                    authResult = await app.AcquireTokenSilent(new[] { Scope }, account).ExecuteAsync();
                    break;
                }
                catch (MsalUiRequiredException)
                {
                }
            }

            if (authResult == null)
            {
                authResult = await app.AcquireTokenInteractive(new[] { Scope }).ExecuteAsync();
            }

            var msaToken = authResult?.AccessToken;
            if (string.IsNullOrEmpty(msaToken))
            {
                Diag("MSAL: no token returned");
                return false;
            }

            _pop = new PopCryptoProvider();
            var deviceToken = await GetDeviceTokenAsync(msaToken, _pop);
            if (deviceToken == null) { Diag("device token failed"); return false; }

            var (userToken, uhs) = await GetUserTokenAsync(msaToken);
            if (userToken == null || string.IsNullOrEmpty(uhs)) { Diag("user token failed"); return false; }

            var titleToken = await GetTitleTokenAsync(msaToken, deviceToken, _pop);
            Diag(titleToken != null ? "title token OK" : "title token failed (continuing without)");

            var (mainXsts, xuid, _) = await GetXstsAsync(userToken, deviceToken, XboxRp, titleToken);
            if (mainXsts == null) { Diag("main XSTS failed"); return false; }
            var xbl = $"XBL3.0 x={uhs};{mainXsts}";

            var (eventsXsts, _, _) = await GetXstsAsync(userToken, deviceToken, EventsRp);

            _cachedXblToken = xbl;
            _cachedSpoofToken = xbl;
            _cachedEventsToken = eventsXsts != null ? $"x:XBL3.0 x={uhs};{eventsXsts}" : null;
            _cachedXuid = xuid;
            _cachedUhs = uhs;
            _cachedAt = DateTime.UtcNow;
            Diag("login OK");
            return true;
        }
        catch (Exception e) { Diag("LoginAsync exception: " + e.Message); return false; }
    }

    public static async Task<bool> LogoutAsync()
    {
        try
        {
            var app = GetMsalApp();
            var accounts = await app.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await app.RemoveAsync(account);
            }
            _cachedXblToken = null;
            _cachedEventsToken = null;
            _cachedXuid = null;
            _cachedUhs = null;
            _cachedSpoofToken = null;
            _pop = null;
            Diag("logout OK");
            return true;
        }
        catch (Exception e) { Diag("LogoutAsync exception: " + e.Message); return false; }
    }

    public static string? GetXblToken() => IsLoggedIn ? _cachedXblToken : null;

    public static string? ManualEventsToken
    {
        get
        {
            if (_manualEventsToken != null) return _manualEventsToken;
            try { _manualEventsToken = SecureStorage.GetAsync(ManualEventsTokenKey).GetAwaiter().GetResult(); }
            catch { _manualEventsToken = null; }
            return _manualEventsToken;
        }
        set
        {
            _manualEventsToken = value;
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                    SecureStorage.Remove(ManualEventsTokenKey);
                else
                    SecureStorage.SetAsync(ManualEventsTokenKey, value).GetAwaiter().GetResult();
            }
            catch { }
        }
    }

    public static bool HasManualEventsToken => !string.IsNullOrWhiteSpace(ManualEventsToken);

    public static string? GetEventsToken()
    {
        if (HasManualEventsToken) return _manualEventsToken;
        return IsLoggedIn ? _cachedEventsToken : null;
    }

    public static string? GetSpoofToken() => IsLoggedIn ? (_cachedSpoofToken ?? _cachedXblToken) : null;

    public static string? SignRequest(string method, string uri, string body) =>
        _pop?.SignRequest(method, uri, _cachedXblToken ?? "", body);

    public static string? SignSpoofRequest(string method, string uri, string body) =>
        _pop?.SignRequest(method, uri, GetSpoofToken() ?? "", body);

    private static async Task<string?> GetDeviceTokenAsync(string msaToken, PopCryptoProvider pop)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            { "Properties", new Dictionary<string, object>
                {
                    { "AuthMethod", "RPS" },
                    { "SiteName", "user.auth.xboxlive.com" },
                    { "RpsTicket", "t=" + msaToken },
                    { "Version", "1.0.0" },
                    { "ProofKey", pop.ProofKey }
                }
            },
            { "RelyingParty", DeviceRp },
            { "TokenType", "JWT" }
        });
        var req = new HttpRequestMessage(HttpMethod.Post, DeviceUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Signature", pop.SignRequest("POST", DeviceUrl, "", body));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var resp = await SignedHttp.SendAsync(req);
        var rb = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) { Diag($"device {(int)resp.StatusCode}: {Trunc(rb)}"); return null; }
        return JsonDocument.Parse(rb).RootElement.GetProperty("Token").GetString();
    }

    private static async Task<string?> GetTitleTokenAsync(string msaToken, string deviceToken, PopCryptoProvider pop)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            { "Properties", new Dictionary<string, object>
                {
                    { "AuthMethod", "RPS" },
                    { "DeviceToken", deviceToken },
                    { "RpsTicket", "t=" + msaToken },
                    { "SiteName", "user.auth.xboxlive.com" },
                    { "ProofKey", pop.ProofKey }
                }
            },
            { "RelyingParty", DeviceRp },
            { "TokenType", "JWT" }
        });
        var req = new HttpRequestMessage(HttpMethod.Post, TitleAuthUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Signature", pop.SignRequest("POST", TitleAuthUrl, "", body));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var resp = await SignedHttp.SendAsync(req);
        var rb = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) { Diag($"title.auth {(int)resp.StatusCode}: {Trunc(rb)}"); return null; }
        return JsonDocument.Parse(rb).RootElement.GetProperty("Token").GetString();
    }

    private static async Task<(string? token, string uhs)> GetUserTokenAsync(string msaToken)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            { "Properties", new Dictionary<string, object>
                {
                    { "AuthMethod", "RPS" },
                    { "SiteName", "user.auth.xboxlive.com" },
                    { "RpsTicket", "t=" + msaToken }
                }
            },
            { "RelyingParty", "http://auth.xboxlive.com" },
            { "TokenType", "JWT" }
        });
        var (code, resp) = await PostAsync(UserUrl, body);
        if (code != 200) return (null, "");
        var d = JsonDocument.Parse(resp).RootElement;
        return (d.GetProperty("Token").GetString(),
                d.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString() ?? "");
    }

    private static async Task<(string? token, string? xuid, string? uhs)> GetXstsAsync(
        string userToken, string deviceToken, string rp, string? titleToken = null)
    {
        object props = string.IsNullOrEmpty(titleToken)
            ? new Dictionary<string, object>
            {
                { "SandboxId", "RETAIL" },
                { "UserTokens", new[] { userToken } },
                { "DeviceToken", deviceToken }
            }
            : new Dictionary<string, object>
            {
                { "SandboxId", "RETAIL" },
                { "UserTokens", new[] { userToken } },
                { "DeviceToken", deviceToken },
                { "TitleToken", titleToken }
            };
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            { "Properties", props },
            { "RelyingParty", rp },
            { "TokenType", "JWT" }
        });
        var (code, resp) = await PostAsync(XstsUrl, body);
        if (code != 200) { Diag($"XSTS({rp}) {code}: {Trunc(resp)}"); return (null, null, null); }
        var d = JsonDocument.Parse(resp).RootElement;
        var xui = d.GetProperty("DisplayClaims").GetProperty("xui")[0];
        return (d.GetProperty("Token").GetString(),
                xui.TryGetProperty("xid", out var x) ? x.GetString() : null,
                xui.TryGetProperty("uhs", out var u) ? u.GetString() : null);
    }

    private static string Trunc(string s) => s.Length > 400 ? s.Substring(0, 400) : s;

    private static async Task<(int code, string body)> PostAsync(string url, string json)
    {
        using var resp = await Http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
        return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }
}

public sealed class PopCryptoProvider
{
    private readonly ECDsa _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private object? _proofKey;
    public object ProofKey => _proofKey ??= BuildProofKey();

    private object BuildProofKey()
    {
        var p = _signer.ExportParameters(false);
        return new Dictionary<string, object>
        {
            { "kty", "EC" },
            { "crv", "P-256" },
            { "alg", "ES256" },
            { "use", "sig" },
            { "x", B64Url(p.Q.X) },
            { "y", B64Url(p.Q.Y) }
        };
    }

    public string SignRequest(string method, string reqUri, string token, string body)
    {
        var winTs = ((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 11644473600ul) * 10000000ul;
        var pathQuery = new Uri(reqUri).PathAndQuery;
        var strs = Encoding.ASCII.GetBytes($"{method}\0{pathQuery}\0{token}\0{body}\0");
        var payload = new byte[4 + 1 + 8 + 1 + strs.Length];
        BeInt(1).CopyTo(payload, 0);
        payload[4] = 0;
        BeULong(winTs).CopyTo(payload, 5);
        payload[13] = 0;
        strs.CopyTo(payload, 14);
        var sig = _signer.SignData(payload, HashAlgorithmName.SHA256);
        var header = new byte[12 + sig.Length];
        BeInt(1).CopyTo(header, 0);
        BeULong(winTs).CopyTo(header, 4);
        sig.CopyTo(header, 12);
        return Convert.ToBase64String(header);
    }

    private static byte[] BeInt(int v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] BeULong(ulong v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static string B64Url(byte[] d) =>
        Convert.ToBase64String(d).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
