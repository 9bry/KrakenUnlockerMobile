using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace KrakenMobile.Services;

public static class ProtectionService
{
    private static bool _isInitialized;
    private static Timer? _watchdogTimer;
    private static bool _shutdownRequested;
    private static readonly object _lock = new();

    private static readonly string[] _suspiciousPackages = new[]
    {
        "com.topjohnwu.magisk",
        "eu.chainfire.supersu",
        "com.koushikdutta.superuser",
        "com.thirdparty.superuser",
        "com.noshufou.android.su",
        "com.devadvance.rootcloak",
        "com.devadvance.rootcloakplus",
        "com.saurik.substrate",
        "org.meowcat.edxposed.manager",
        "org.lsposed.manager",
        "com.rechild.advancedrootpermission",
        "eu.chainfire.magisk",
        "com.kingo.root",
        "com.root.master",
        "com.dianxinos.optimizer.dupclean",
        "com.qihoo360.rewire",
        "com.kingroot.kinguser",
        "com.kingo.root.su",
        "com.topjohnwu.magisk.manager",
        "io.github.vvb2060.magisk",
        "me.weishu.kernelsu",
        "com.tsng.hidemyapplist",
        "org.lsposed.lspd"
    };

    private static readonly string[] _cheatApps = new[]
    {
        "com.gameguardian",
        "com.chelpus.xposedinstaller",
        "com.xposedinstaller.xposedinstaller",
        "me.weishu.kernelsu",
        "com.tsng.hidemyapplist",
        "com.lbe.parallel.intl",
        "com.parallel.space.lite"
    };

    private static readonly string[] _debuggerIndicators = new[]
    {
        "com.frida.server",
        "com.frida.gadget",
        "de.robv.android.xposed.installer"
    };

    // Expected signing-cert SHA-256, split to avoid a single obvious constant.
    private static readonly string[] _hashChunks = new[]
    {
        "09ed3423b43a4afaa1c129f502b5d4e3",
        "af3035045c054112a378019e50090735"
    };

    public static ProtectionResult Check()
    {
        var result = new ProtectionResult();

        result.IsRooted = CheckRoot();
        result.HasSuspiciousApps = CheckSuspiciousApps();
        result.HasCheatApps = CheckCheatApps();
        result.IsDebugged = CheckDebugger();
        result.SignatureValid = VerifySignature();
        result.HasDebuggingTools = CheckDebugTools();
        result.HasFrida = CheckFrida();
        result.IsDebuggableBuild = CheckDebuggable();

        result.IsTampered = result.IsRooted || result.HasSuspiciousApps ||
                           result.HasCheatApps || !result.SignatureValid ||
                           result.HasDebuggingTools || result.HasFrida ||
                           result.IsDebuggableBuild;

        return result;
    }

    public static void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized) return;
            _isInitialized = true;
        }

        _watchdogTimer = new Timer(async _ => await WatchdogCallback(), null,
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));
    }

    private static async Task WatchdogCallback()
    {
        if (_shutdownRequested) return;

        try
        {
            var result = Check();
            if (result.IsTampered)
            {
                RequestShutdown();
                return;
            }

            var sessionValid = SecurityService.ValidateSession();
            if (!sessionValid && LicenseService.IsPremium)
            {
                RequestShutdown();
                return;
            }
        }
        catch
        {
            RequestShutdown();
        }
    }

    private static void RequestShutdown()
    {
        lock (_lock)
        {
            if (_shutdownRequested) return;
            _shutdownRequested = true;
        }

        try
        {
            _watchdogTimer?.Dispose();
        }
        catch { }

        try
        {
            var activity = Platform.CurrentActivity;
            if (activity != null)
            {
                activity.RunOnUiThread(() =>
                {
                    try
                    {
                        activity.FinishAffinity();
                        Java.Lang.JavaSystem.Exit(0);
                    }
                    catch
                    {
                        Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
                    }
                });
            }
            else
            {
                Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
            }
        }
        catch
        {
            Environment.Exit(1);
        }
    }

    public static bool CheckRoot()
    {
        try
        {
            var paths = new[]
            {
                "/system/bin/su", "/system/xbin/su", "/sbin/su",
                "/data/local/xbin/su", "/data/local/bin/su",
                "/system/sd/xbin/su", "/system/bin/failsafe/su",
                "/data/local/su", "/su/bin/su",
                "/system/app/Superuser.apk",
                "/system/app/SuperSU.apk",
                "/data/adb/magisk",
                "/data/adb/magisk.img"
            };

            foreach (var path in paths)
            {
                if (System.IO.File.Exists(path))
                    return true;
            }

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "su",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process != null)
                {
                    process.WaitForExit(1000);
                    if (process.ExitCode == 0)
                        return true;
                }
            }
            catch { }

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "su",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(1000);
                    if (!string.IsNullOrEmpty(output) && output.Contains("/su"))
                        return true;
                }
            }
            catch { }

            return false;
        }
        catch { return false; }
    }

    private static bool CheckSuspiciousApps()
    {
        try
        {
            var pm = Android.App.Application.Context.PackageManager;
            if (pm == null) return false;

            foreach (var pkg in _suspiciousPackages)
            {
                try
                {
                    pm.GetPackageInfo(pkg, 0);
                    return true;
                }
                catch { }
            }
            return false;
        }
        catch { return false; }
    }

    private static bool CheckCheatApps()
    {
        try
        {
            var pm = Android.App.Application.Context.PackageManager;
            if (pm == null) return false;

            foreach (var pkg in _cheatApps)
            {
                try
                {
                    pm.GetPackageInfo(pkg, 0);
                    return true;
                }
                catch { }
            }
            return false;
        }
        catch { return false; }
    }

    private static bool CheckDebugger()
    {
        try
        {
            if (Debugger.IsAttached) return true;
            if (Debugger.IsLogging()) return true;

            try
            {
                var pid = Android.OS.Process.MyPid();
                var statusPath = $"/proc/{pid}/status";
                if (System.IO.File.Exists(statusPath))
                {
                    var status = System.IO.File.ReadAllText(statusPath);
                    var lines = status.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("TracerPid:"))
                        {
                            var val = line.Split('\t').LastOrDefault()?.Trim();
                            if (val != "0" && !string.IsNullOrEmpty(val))
                                return true;
                        }
                    }
                }
            }
            catch { }

            try
            {
                var pid = Android.OS.Process.MyPid();
                var fdPath = $"/proc/{pid}/fd";
                if (System.IO.Directory.Exists(fdPath))
                {
                    var fds = System.IO.Directory.GetFiles(fdPath);
                    if (fds.Length > 1024)
                        return true;
                }
            }
            catch { }

            return false;
        }
        catch { return false; }
    }

    private static bool CheckDebugTools()
    {
        try
        {
            var pm = Android.App.Application.Context.PackageManager;
            if (pm == null) return false;

            foreach (var pkg in _debuggerIndicators)
            {
                try
                {
                    pm.GetPackageInfo(pkg, 0);
                    return true;
                }
                catch { }
            }
            return false;
        }
        catch { return false; }
    }

    private static bool CheckFrida()
    {
        try
        {
            var mapsPath = $"/proc/{Android.OS.Process.MyPid()}/maps";
            if (System.IO.File.Exists(mapsPath))
            {
                var maps = System.IO.File.ReadAllText(mapsPath);
                if (maps.Contains("frida", StringComparison.OrdinalIgnoreCase) ||
                    maps.Contains("gadget", StringComparison.OrdinalIgnoreCase) ||
                    maps.Contains("substrate", StringComparison.OrdinalIgnoreCase) ||
                    maps.Contains("xposed", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }

        try
        {
            var tmp = "/data/local/tmp";
            if (System.IO.Directory.Exists(tmp))
            {
                foreach (var f in System.IO.Directory.GetFiles(tmp))
                {
                    if (System.IO.Path.GetFileName(f).StartsWith("frida", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch { }

        return false;
    }

    private static bool CheckDebuggable()
    {
        try
        {
            var context = Android.App.Application.Context;
            var pm = context.PackageManager;
            if (pm == null) return false;

            var appInfo = pm.GetApplicationInfo(context.PackageName!, 0);
            return (appInfo.Flags & Android.Content.PM.ApplicationInfoFlags.Debuggable) != 0;
        }
        catch { return false; }
    }

    public static bool VerifySignature()
    {
        try
        {
            var context = Android.App.Application.Context;
            var packageName = context.PackageName;
            var pm = context.PackageManager;
            if (pm == null || packageName == null) return false;

            var packageInfo = pm.GetPackageInfo(packageName, Android.Content.PM.PackageInfoFlags.Signatures);
            if (packageInfo?.Signatures == null || packageInfo.Signatures.Count == 0) return false;

            var sig = packageInfo.Signatures[0];
            var sigBytes = sig.ToByteArray();

            using var sha = SHA256.Create();
            var digest = sha.ComputeHash(sigBytes);
            var currentHash = BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();

            var expectedHash = GetExpectedSignatureHash();
            if (string.IsNullOrEmpty(expectedHash))
            {
                _lastKnownHash = currentHash;
                return true;
            }

            return string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    private static string? _lastKnownHash = string.Concat(_hashChunks);

    private static string? GetExpectedSignatureHash()
    {
        return _lastKnownHash;
    }

    public static string GetSignatureHash()
    {
        try
        {
            var context = Android.App.Application.Context;
            var packageName = context.PackageName;
            var pm = context.PackageManager;
            if (pm == null || packageName == null) return "unknown";

            var packageInfo = pm.GetPackageInfo(packageName, Android.Content.PM.PackageInfoFlags.Signatures);
            if (packageInfo?.Signatures == null || packageInfo.Signatures.Count == 0) return "unknown";

            var sig = packageInfo.Signatures[0];
            var sigBytes = sig.ToByteArray();

            using var sha = SHA256.Create();
            var digest = sha.ComputeHash(sigBytes);
            return BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
        }
        catch { return "unknown"; }
    }

    public static void LockSignature()
    {
        if (string.IsNullOrEmpty(_lastKnownHash))
        {
            _lastKnownHash = GetSignatureHash();
        }
    }

    public static void StopWatchdog()
    {
        _watchdogTimer?.Dispose();
        _watchdogTimer = null;
    }
}

public class ProtectionResult
{
    public bool IsRooted { get; set; }
    public bool HasSuspiciousApps { get; set; }
    public bool HasCheatApps { get; set; }
    public bool IsDebugged { get; set; }
    public bool SignatureValid { get; set; }
    public bool HasDebuggingTools { get; set; }
    public bool HasFrida { get; set; }
    public bool IsDebuggableBuild { get; set; }
    public bool IsTampered { get; set; }

    public string GetSummary()
    {
        var issues = new List<string>();
        if (IsRooted) issues.Add("Root detected");
        if (HasSuspiciousApps) issues.Add("Suspicious apps found");
        if (HasCheatApps) issues.Add("Cheat apps detected");
        if (IsDebugged) issues.Add("Debugger attached");
        if (!SignatureValid) issues.Add("Invalid signature");
        if (HasDebuggingTools) issues.Add("Debugging tools found");
        if (HasFrida) issues.Add("Frida detected");
        if (IsDebuggableBuild) issues.Add("Debuggable build");
        return issues.Count == 0 ? "All checks passed" : string.Join("; ", issues);
    }
}
