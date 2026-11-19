using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Pclge.Core;

public record JavaInfo(string Exe, string Home, string Version)
{
    public bool Is64Bit => Version.Contains("64-Bit") || Version.Contains("64-bit");
    public int Major
    {
        get
        {
            var m = Regex.Match(Version, @"(\d+)");
            if (!m.Success) return 0;
            var n = int.Parse(m.Groups[1].Value);
            // 1.8.0_xxx 需要看第二段
            if (n == 1)
            {
                var all = Regex.Matches(Version, @"(\d+)");
                if (all.Count > 2) return int.Parse(all[1].Groups[1].Value);
            }
            return n;
        }
    }

    public string Display => $"{Version}  ({Exe})";
}

/// <summary>Java 发现与版本探测。</summary>
public static class JavaService
{
    private static List<JavaInfo> _cache;

    public static List<JavaInfo> Scan(bool force = false)
    {
        if (_cache != null && !force) return _cache;
        var homes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddHome(string home)
        {
            if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home)) homes.Add(home);
        }

        var configured = Config.JavaPath;
        if (configured.Length > 0) AddHome(Path.GetDirectoryName(Path.GetDirectoryName(configured)) ?? "");

        AddHome(Environment.GetEnvironmentVariable("JAVA_HOME") ?? "");

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                var trimmed = dir.Trim();
                if (Directory.Exists(Path.Combine(trimmed, "bin"))) AddHome(trimmed);
            }
            catch { }
        }

        var roots = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Foundation"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zulu"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BellSoft"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Amazon Corretto"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Semeru"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Eclipse Adoptium"),
            @"C:\Program Files (x86)\Java"
        };
        foreach (var root in roots)
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var home in Directory.GetDirectories(root))
                {
                    AddHome(home);
                    foreach (var sub in Directory.GetDirectories(home))
                        if (Directory.Exists(Path.Combine(sub, "bin"))) AddHome(sub);
                }
            }
            catch { }
        }

        var list = new List<JavaInfo>();
        foreach (var home in homes)
        {
            var javaw = Path.Combine(home, "bin", "javaw.exe");
            var java = Path.Combine(home, "bin", "java.exe");
            if (!File.Exists(javaw) && !File.Exists(java)) continue;
            var exe = File.Exists(javaw) ? javaw : java;
            list.Add(new JavaInfo(exe, home, ProbeVersion(exe)));
        }

        list.Sort((a, b) => b.Major - a.Major);
        _cache = list;
        return list;
    }

    public static string ProbeVersion(string javaExe)
    {
        try
        {
            var psi = new ProcessStartInfo(javaExe, "-version")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var err = p.StandardError.ReadToEnd();
            var output = (err + p.StandardOutput.ReadToEnd()).Split('\n')
                .FirstOrDefault(l => l.Contains("version", StringComparison.OrdinalIgnoreCase))?.Trim() ?? "";
            p.WaitForExit(4000);
            return output.Replace("openjdk version ", "").Replace("java version ", "").Trim('"', ' ', '\r');
        }
        catch
        {
            return "未知版本";
        }
    }

    /// <summary>优先使用配置的 Java，否则用版本号最高的一个。</summary>
    public static string Resolve()
    {
        var configured = Config.JavaPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var list = Scan();
        return list.Count > 0 ? list[0].Exe : "javaw.exe";
    }

    /// <summary>按 Minecraft 版本要求挑选 Java：1.17+ 需要 17，1.20.5+ 建议 21。</summary>
    public static string ResolveFor(string versionId)
    {
        var configured = Config.JavaPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        int need = 8;
        if (versionId.Contains("1.17") || versionId.Contains("1.18") || versionId.Contains("1.19")) need = 17;
        else if (versionId.Contains("1.20.5") || versionId.Contains("1.20.6") || versionId.Contains("1.21") || versionId.Contains("1.22") || versionId.Contains("26.")) need = 21;
        var list = Scan().Where(j => j.Major >= need).OrderBy(j => j.Major).ToList();
        return list.Count > 0 ? list[0].Exe : Resolve();
    }
}
