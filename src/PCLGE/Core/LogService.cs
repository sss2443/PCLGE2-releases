using System.IO;
using System.Text.Json;

namespace Pclge.Core;

/// <summary>PCLGE 运行日志：内存缓冲 + 文件落盘（%AppData%\PCLGE\logs\latest.log）。</summary>
public static class LogService
{
    private static readonly List<string> Lines = new();
    private static readonly object Lock = new();
    private static string _file;

    public static event Action<string> OnLine;

    public static string LogFile
    {
        get
        {
            if (_file != null) return _file;
            Directory.CreateDirectory(Config.LogDir);
            _file = Path.Combine(Config.LogDir, "latest.log");
            return _file;
        }
    }

    public static bool FileLogEnabled => Config.GetBool("fileLog", true);

    public static void Write(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        lock (Lock)
        {
            Lines.Add(line);
            if (Lines.Count > 5000) Lines.RemoveRange(0, Lines.Count - 5000);
        }
        OnLine?.Invoke(line);
        if (!FileLogEnabled) return;
        try
        {
            Directory.CreateDirectory(Config.LogDir);
            File.AppendAllText(LogFile, line + Environment.NewLine);
        }
        catch { }
    }

    public static List<string> Snapshot()
    {
        lock (Lock) return Lines.ToList();
    }

    public static void Clear()
    {
        lock (Lock) Lines.Clear();
        try { if (File.Exists(LogFile)) File.Delete(LogFile); } catch { }
    }

    /// <summary>扫描日志里的启动器记录，估算最近一次启动结果（供崩溃分析使用）。</summary>
    public static List<string> RecentLaunches()
    {
        var list = new List<string>();
        foreach (var line in Snapshot().Where(l => l.Contains("游戏进程已启动") || l.Contains("游戏已退出"))) list.Add(line);
        return list;
    }
}
