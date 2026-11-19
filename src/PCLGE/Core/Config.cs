using System.IO;

namespace Pclge.Core;

/// <summary>启动器配置与常用目录。配置文件位于 %AppData%\PCLGE\pclge-config.ini。</summary>
public static class Config
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCLGE");

    private static readonly string FilePath = Path.Combine(Dir, "pclge-config.ini");

    private static readonly Dictionary<string, string> Map = Load();

    private static Dictionary<string, string> Load()
    {
        var d = new Dictionary<string, string>();
        try
        {
            Directory.CreateDirectory(Dir);
            if (File.Exists(FilePath))
            {
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var i = line.IndexOf('=');
                    if (i > 0) d[line[..i]] = line[(i + 1)..];
                }
            }
        }
        catch { }
        return d;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllLines(FilePath, Map.Select(kv => kv.Key + "=" + kv.Value));
        }
        catch { }
    }

    public static string LauncherDir => Dir;
    public static string ConfigFile => FilePath;

    public static string Get(string key, string def) => Map.TryGetValue(key, out var v) ? v : def;

    public static void Put(string key, string value)
    {
        Map[key] = value ?? "";
        Save();
    }

    public static bool GetBool(string key, bool def) => Get(key, def ? "1" : "0") == "1";

    public static int GetInt(string key, int def)
    {
        var raw = Get(key, "");
        return int.TryParse(raw, out var v) ? v : def;
    }

    // ---- 游戏目录 ----
    public static string DefaultGameDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

    public static string GameDir
    {
        get
        {
            var custom = Get("gameDir", "");
            return custom.Length > 0 ? custom : DefaultGameDir;
        }
    }

    public static void SetGameDir(string path)
    {
        Directory.CreateDirectory(path);
        Put("gameDir", path);
    }

    public static void ResetGameDir() => Put("gameDir", "");

    /// <summary>版本隔离：打开后每个版本使用独立的 gameDir（versions\&lt;版本&gt;\.minecraft）。</summary>
    public static bool Isolation => GetBool("isolation", false);

    public static string GameDirFor(string versionId) =>
        Isolation ? Path.Combine(VersionsDir, versionId, ".minecraft") : GameDir;

    public static string VersionsDir => Path.Combine(GameDir, "versions");
    public static string LibrariesDir => Path.Combine(GameDir, "libraries");
    public static string AssetsDir => Path.Combine(GameDir, "assets");
    public static string SkinsDir => Path.Combine(LauncherDir, "skins");
    public static string LogDir => Path.Combine(LauncherDir, "logs");
    public static string InstallerDir => Path.Combine(LauncherDir, "installers");

    // ---- 玩家与 Java ----
    public static string PlayerName => Get("playerName", "Steve");
    public static int MemoryMb => GetInt("memory", 2048);
    public static string JvmArgs => Get("jvmArgs", "");
    public static string JavaPath => Get("javaPath", "");
    public static bool UseBmcl => Get("source", "1") == "1";
    public static string MetaBase => UseBmcl ? "https://bmclapi2.bangbang93.com/" : "https://launchermeta.mojang.com/";

    public static int DownloadThreads => Math.Clamp(GetInt("threads", 12), 1, 32);
}
