using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Pclge.Core;

public record Finding(string Title, string Detail, string Advice);

/// <summary>崩溃分析：读取 crash-reports 与 logs\latest.log，匹配常见原因。</summary>
public static class CrashAnalyzer
{
    private record Rule(string Key, string Title, string Advice);

    private static readonly Rule[] Rules =
    {
        new("java.lang.OutOfMemoryError", "内存不足",
            "把「设置 → 启动器 → 内存」调大（1.20+ 建议 4096MB 以上），或减少模组数量。"),
        new("UnsupportedClassVersionError", "Java 版本过低",
            "该版本/模组要求更高版本 Java，在「设置 → Java」里换成 Java 17 / 21。"),
        new("Could not find or load main class", "缺少主类或依赖库不完整",
            "重新安装该版本（下载页 → 已安装 → 修复），确认 libraries 目录未被误删。"),
        new("NoSuchMethodError", "模组与游戏版本不兼容",
            "某个模组调用了不存在的 API：检查模组支持的 MC 版本，逐个禁用排查。"),
        new("NoClassDefFoundError", "缺少前置模组",
            "该模组缺少依赖（Fabric API / Cloth Config 等），到下载页补装对应前置。"),
        new("ModResolutionException", "模组依赖冲突",
            "有两个模组互相要求不同版本，禁用其中一个，或统一升级到兼容版本。"),
        new("Duplicate mods", "模组重复",
            "mods 目录里有同一个模组的多个版本，删掉旧版本再启动。"),
        new("java.lang.ClassNotFoundException", "类缺失（多为前置模组缺失）",
            "安装缺失的前置模组，或确认该模组确实适配当前加载器。"),
        new("GLFW error", "显卡驱动 / OpenGL 问题",
            "更新显卡驱动；笔记本切换到独显运行；或尝试关闭光影。"),
        new("OpenGL", "显卡驱动 / OpenGL 问题",
            "更新显卡驱动，确认没有把游戏放到远程桌面中运行。"),
        new("AccessDeniedException", "文件被占用或没有权限",
            "关闭杀毒软件拦截，或把游戏目录换到非系统盘（例如 D:\\Minecraft）。"),
        new("The game crashed whilst", "游戏崩溃（需看具体报告）",
            "展开下方崩溃报告，搜索 Exception 前的几行，通常会写明是哪个模组导致的。"),
        new("Exit code: -1073741819", "访问冲突（内存/驱动问题）",
            "常见于 32 位 Java 或驱动冲突：换 64 位 Java，关闭超频，更新驱动。"),
        new("Could not reserve enough space", "内存分配失败",
            "内存设置超过系统可用内存或用了 32 位 Java，调小内存并换 64 位 Java。"),
        new("Failed to download", "文件缺失",
            "资源/依赖没下载完整，重新安装该版本。"),
    };

    public static string LatestCrashReport(string versionId)
    {
        var dir = VersionService.CrashReportDir(versionId);
        if (!Directory.Exists(dir)) return null;
        var file = new DirectoryInfo(dir).GetFiles("*.txt").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
        return file?.FullName;
    }

    public static List<Finding> Analyze(string versionId)
    {
        var findings = new List<Finding>();
        var gameDir = Config.GameDirFor(versionId);
        var text = new StringBuilder();

        var crash = LatestCrashReport(versionId);
        if (crash != null)
        {
            try { text.AppendLine(File.ReadAllText(crash)); } catch { }
        }

        var latestLog = Path.Combine(gameDir, "logs", "latest.log");
        if (File.Exists(latestLog))
        {
            try
            {
                // 只取最后 3000 行，避免超大日志
                var lines = File.ReadAllLines(latestLog);
                var tail = lines.Length > 3000 ? lines[^3000..] : lines;
                text.AppendLine(string.Join("\n", tail));
            }
            catch { }
        }

        if (text.Length == 0)
        {
            findings.Add(new Finding("没有可分析的文件",
                "未找到 crash-reports/*.txt 或 logs/latest.log。",
                "先在启动页选好版本并启动一次，崩溃后再回到这里分析。"));
            return findings;
        }

        var content = text.ToString();
        foreach (var rule in Rules)
        {
            if (!content.Contains(rule.Key, StringComparison.OrdinalIgnoreCase)) continue;
            if (findings.Any(f => f.Title == rule.Title)) continue;
            findings.Add(new Finding(rule.Title, ExtractEvidence(content, rule.Key), rule.Advice));
        }

        // 内存信息
        var memMatch = Regex.Match(content, @"Memory:\s*(\d+)\s*MB");
        if (memMatch.Success && int.TryParse(memMatch.Groups[1].Value, out var mem) && mem < 1024)
            findings.Add(new Finding("分配内存偏小", $"崩溃时只分配了 {mem}MB 内存。", "建议至少 2048MB，模组整合包建议 4096MB 以上。"));

        // 列出加载失败的模组
        var modFail = Regex.Matches(content, @"Failed to load mod(?:s)?[:\s]+(.+)");
        if (modFail.Count > 0)
        {
            var names = modFail.Select(m => m.Groups[1].Value.Trim()).Distinct().Take(5).ToList();
            findings.Add(new Finding("模组加载失败", string.Join("；", names), "禁用这些模组，或换用适配当前版本的版本。"));
        }

        if (findings.Count == 0)
            findings.Add(new Finding("未匹配到已知原因",
                "日志里没有命中常见崩溃关键词。",
                "把 crash-reports 里的 txt 打开，搜索 Exception 或 Caused by 看第一处报错。"));
        return findings;
    }

    private static string ExtractEvidence(string content, string key)
    {
        var index = content.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return "";
        var start = Math.Max(0, index - 80);
        var length = Math.Min(300, content.Length - start);
        return content.Substring(start, length).Replace("\r", "").Trim();
    }

    /// <summary>导出最近一次崩溃报告到桌面，返回路径。</summary>
    public static string ExportLatest(string versionId)
    {
        var crash = LatestCrashReport(versionId);
        if (crash == null) return null;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var dest = Path.Combine(desktop, $"PCLGE-崩溃报告-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.Copy(crash, dest, true);
        return dest;
    }
}
