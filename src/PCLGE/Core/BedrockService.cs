using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Pclge.Core;

/// <summary>基岩版（BDS）服务端：下载、关联、启动、配置；以及基岩版客户端（UWP）检测、安装、启动。</summary>
public static class BedrockService
{
    private const string DownloadPage = "https://www.minecraft.net/en-us/download/server/bedrock";

    public static string AssociatedDir => Config.Get("bedrockDir", "");

    public static Process Current { get; private set; }

    public static bool IsRunning => Current is { HasExited: false };

    /// <summary>从官网下载页解析 Windows 版 BDS 压缩包地址。</summary>
    public static string ResolveWindowsDownloadUrl()
    {
        var html = Net.GetText(DownloadPage);
        var match = Regex.Match(html,
            @"https://www\.minecraft\.net/bedrockdedicatedserver/bin-win/[^""'\s>]+\.zip");
        if (!match.Success)
            throw new Exception("没能在官网页面找到 Windows 版 BDS 下载地址，可能页面结构变化，请手动下载");
        return match.Value;
    }

    /// <summary>下载并解压 BDS 到目标目录，返回目录。</summary>
    public static string Download(string targetDir, Action<string> log)
    {
        Directory.CreateDirectory(targetDir);
        var url = ResolveWindowsDownloadUrl();
        log("BDS 地址：" + url);
        var zip = Path.Combine(Config.InstallerDir, "bedrock-server.zip");
        Net.Download(url, zip, (d, t) => log($"下载中 {d / 1048576.0:F1}MB"));
        log("解压到 " + targetDir);
        ZipFile.ExtractToDirectory(zip, targetDir, true);
        Config.Put("bedrockDir", targetDir);
        log("基岩版服务端已安装：" + targetDir);
        return targetDir;
    }

    public static void Start(Action<string> log)
    {
        var dir = AssociatedDir;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            throw new Exception("还没有关联 BDS 目录");
        var exe = Path.Combine(dir, "bedrock_server.exe");
        if (!File.Exists(exe)) throw new Exception("目录里没有 bedrock_server.exe");

        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var proc = Process.Start(psi) ?? throw new Exception("启动失败");
        Current = proc;
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) log("[BDS] " + e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) log("[BDS] " + e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        log("基岩版服务端已启动，PID " + proc.Id);
    }

    public static void Stop()
    {
        if (!IsRunning) return;
        try { Current?.Kill(true); } catch { }
        Current = null;
    }

    public static string PropertiesPath => Path.Combine(AssociatedDir, "server.properties");

    public static string ReadProperties()
    {
        var path = PropertiesPath;
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public static void WriteProperties(string content) => File.WriteAllText(PropertiesPath, content);

    // ================= 基岩版客户端（UWP）=================

    /// <summary>商店里的「Minecraft for Windows」产品 ID。</summary>
    public const string StoreProductId = "9NBLGGH2JHXJ";

    public const string StorePageUrl = "ms-windows-store://pdp/?ProductId=" + StoreProductId;
    public const string StoreSearchUrl = "ms-windows-store://search/?query=Minecraft";

    public record ClientInfo(string PackageName, string Version, string Location)
    {
        public bool IsPreview => PackageName.Contains("WindowsBeta", StringComparison.OrdinalIgnoreCase);
        public bool IsEducation => PackageName.Contains("Education", StringComparison.OrdinalIgnoreCase);

        public string Display =>
            (IsPreview ? "Minecraft Preview" : IsEducation ? "Minecraft 教育版" : "Minecraft for Windows")
            + "  " + Version;

        /// <summary>UWP 的 AUMID，用于 shell:AppsFolder 启动。</summary>
        public string AumId => PackageName + "_8wekyb3d8bbwe!App";
    }

    /// <summary>读取已安装的基岩版客户端（Get-AppxPackage）。没装 / 非 Windows 都返回空表。</summary>
    public static List<ClientInfo> DetectClients()
    {
        var list = new List<ClientInfo>();
        if (!OperatingSystem.IsWindows()) return list;
        var json = RunPowerShell(
            "Get-AppxPackage | Where-Object { $_.Name -in @('Microsoft.MinecraftUWP','Microsoft.MinecraftWindowsBeta','Microsoft.MinecraftEducationEdition') } " +
            "| Select-Object Name,Version,InstallLocation | ConvertTo-Json -Compress").Trim();
        if (json.Length == 0 || json == "null") return list;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var e in doc.RootElement.EnumerateArray()) list.Add(Read(e));
            }
            else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                list.Add(Read(doc.RootElement));
            }
        }
        catch
        {
            // JSON 解析失败按未安装处理
        }
        return list;

        static ClientInfo Read(System.Text.Json.JsonElement e) => new(
            e.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
            e.TryGetProperty("Version", out var v) ? v.GetString() ?? "" : "",
            e.TryGetProperty("InstallLocation", out var i) ? i.GetString() ?? "" : "");
    }

    public static bool HasWinget()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var psi = new ProcessStartInfo("where.exe", "winget")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return p.ExitCode == 0 && output.Trim().Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     用 winget 从微软商店安装 / 更新基岩版客户端（正版渠道，需要微软账户已登录商店）。
    ///     返回退出码，0 表示成功。
    /// </summary>
    public static int InstallClient(Action<string> log)
    {
        if (!OperatingSystem.IsWindows()) throw new Exception("只有 Windows 才能安装基岩版客户端");
        if (!HasWinget()) throw new Exception("这台电脑没有 winget，请点「打开商店页面」手动安装");

        var psi = new ProcessStartInfo("winget.exe",
            $"install --id {StoreProductId} --source msstore --accept-package-agreements --accept-source-agreements")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi) ?? throw new Exception("winget 启动失败");
        while (!proc.StandardOutput.EndOfStream)
        {
            var line = proc.StandardOutput.ReadLine();
            if (!string.IsNullOrWhiteSpace(line)) log("[winget] " + line);
        }
        while (!proc.StandardError.EndOfStream)
        {
            var line = proc.StandardError.ReadLine();
            if (!string.IsNullOrWhiteSpace(line)) log("[winget] " + line);
        }
        proc.WaitForExit();
        return proc.ExitCode;
    }

    /// <summary>启动基岩版客户端（正版渠道安装后可直接拉起）。</summary>
    public static void LaunchClient(ClientInfo client)
    {
        if (!OperatingSystem.IsWindows()) throw new Exception("只有 Windows 才能启动基岩版客户端");
        Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{client.AumId}")
        {
            UseShellExecute = true
        });
    }

    private static string RunPowerShell(string script)
    {
        var psi = new ProcessStartInfo("powershell.exe",
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "\\\"") + "\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new Exception("无法调用 PowerShell");
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(15000);
        return output;
    }
}
