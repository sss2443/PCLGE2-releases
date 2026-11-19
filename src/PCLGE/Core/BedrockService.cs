using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Pclge.Core;

/// <summary>基岩版（BDS）服务端：下载、关联、启动、配置。</summary>
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
}
