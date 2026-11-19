using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Pclge.Core;

/// <summary>版本获取、安装、查询、删除。</summary>
public static class VersionService
{
    public record Entry(string Id, string Type, string Url, string Time);

    private static List<Entry> _manifest;

    public static List<Entry> Fetch(bool force = false)
    {
        if (_manifest != null && !force) return _manifest;
        var url = Config.MetaBase + "mc/game/version_manifest_v2.json";
        var text = Net.GetText(url);
        using var doc = JsonDocument.Parse(text);
        var list = new List<Entry>();
        foreach (var v in doc.RootElement.GetProperty("versions").EnumerateArray())
        {
            list.Add(new Entry(
                v.GetProperty("id").GetString()!,
                v.TryGetProperty("type", out var t) ? t.GetString()! : "release",
                v.GetProperty("url").GetString()!,
                v.TryGetProperty("releaseTime", out var rt) ? rt.GetString()! : ""));
        }
        _manifest = list;
        return list;
    }

    public static string LatestSnapshot()
    {
        var list = Fetch();
        var snap = list.FirstOrDefault(v => v.Type == "snapshot");
        return snap?.Id ?? "";
    }

    public static List<string> Installed()
    {
        var list = new List<string>();
        try
        {
            if (!Directory.Exists(Config.VersionsDir)) return list;
            foreach (var dir in Directory.GetDirectories(Config.VersionsDir))
            {
                var id = Path.GetFileName(dir);
                if (Directory.GetFiles(dir, "*.json").Length > 0) list.Add(id);
            }
        }
        catch { }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public record Detail(string Id, string Type, string Loader, string Inherits, long Size);

    public static Detail Inspect(string versionId)
    {
        var dir = Path.Combine(Config.VersionsDir, versionId);
        var json = Path.Combine(dir, versionId + ".json");
        var type = "原版";
        var inherits = "";
        if (File.Exists(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(json));
                inherits = doc.RootElement.TryGetProperty("inheritsFrom", out var p) ? p.GetString()! : "";
                if (inherits.Length == 0 && doc.RootElement.TryGetProperty("type", out var t))
                    type = t.GetString()!.Replace("release", "正式版").Replace("snapshot", "快照");
            }
            catch { }
        }
        var lower = versionId.ToLowerInvariant();
        var loader = lower.Contains("neoforge") ? "NeoForge"
            : lower.Contains("forge") ? "Forge"
            : lower.Contains("quilt") ? "Quilt"
            : lower.Contains("fabric") ? "Fabric"
            : "原版";
        long size = 0;
        try
        {
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                size += new FileInfo(f).Length;
        }
        catch { }
        return new Detail(versionId, type, loader, inherits, size);
    }

    /// <summary>下载并安装一个版本（游戏 JSON、核心 jar、依赖库、资源文件）。</summary>
    public static void Install(string id, string versionUrl, Action<string, double> stage)
    {
        InstallRecursive(id, versionUrl, stage, 0);
    }

    private static void InstallRecursive(string id, string versionUrl, Action<string, double> stage, int depth)
    {
        if (depth > 3) return;
        var dir = Path.Combine(Config.VersionsDir, id);
        Directory.CreateDirectory(dir);
        var jsonPath = Path.Combine(dir, id + ".json");

        stage($"下载版本信息 {id}", 0.02);
        if (!File.Exists(jsonPath)) Net.Download(versionUrl, jsonPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;

        if (root.TryGetProperty("inheritsFrom", out var parent) && parent.GetString() is { } parentId)
        {
            var entry = Fetch().FirstOrDefault(e => e.Id == parentId);
            if (entry != null) InstallRecursive(entry.Id, entry.Url, stage, depth + 1);
        }

        // 游戏核心
        if (root.TryGetProperty("downloads", out var downloads) && downloads.TryGetProperty("client", out var client))
        {
            var jar = Path.Combine(dir, id + ".jar");
            if (!File.Exists(jar))
            {
                stage($"下载游戏核心 {id}.jar", 0.05);
                Net.Download(client.GetProperty("url").GetString()!, jar,
                    (d, t) => stage($"下载游戏核心 {d / 1048576}MB", t > 0 ? 0.05 + 0.45 * d / t : 0.1));
            }
        }

        // 依赖库
        if (root.TryGetProperty("libraries", out var libs))
        {
            var items = libs.EnumerateArray().ToList();
            var index = 0;
            foreach (var lib in items)
            {
                index++;
                if (!Libraries.Allowed(lib)) continue;
                stage($"下载依赖库 {index}/{items.Count}", 0.5 + 0.1 * index / Math.Max(1, items.Count));
                var artifact = Libraries.Resolve(lib);
                if (artifact != null)
                {
                    var dest = Path.Combine(Config.LibrariesDir, artifact.Value.Path);
                    if (!File.Exists(dest))
                    {
                        var url = MirrorLibrary(artifact.Value.Url);
                        Net.DownloadIfMissing(url, dest);
                    }
                }
                var native = Libraries.ResolveNative(lib);
                if (native != null)
                {
                    var dest = Path.Combine(Config.LibrariesDir, native.Value.Path);
                    if (!File.Exists(dest)) Net.DownloadIfMissing(MirrorLibrary(native.Value.Url), dest);
                    ExtractNatives(dest, Path.Combine(dir, "natives"));
                }
            }
        }

        // 资源文件
        if (root.TryGetProperty("assetIndex", out var assetIndex))
        {
            var indexUrl = assetIndex.GetProperty("url").GetString()!;
            var indexId = assetIndex.GetProperty("id").GetString()!;
            var indexDir = Path.Combine(Config.AssetsDir, "indexes");
            Directory.CreateDirectory(indexDir);
            var indexPath = Path.Combine(indexDir, indexId + ".json");
            if (!File.Exists(indexPath))
            {
                stage("下载资源索引", 0.62);
                Net.Download(MirrorAsset(indexUrl), indexPath);
            }
            stage("下载游戏资源（数千小文件，首次较慢）", 0.65);
            try
            {
                using var assetsDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
                var objects = assetsDoc.RootElement.GetProperty("objects").EnumerateObject().ToList();
                var parallel = new ParallelOptions { MaxDegreeOfParallelism = Config.DownloadThreads };
                var done = 0;
                Parallel.ForEach(objects, parallel, item =>
                {
                    var hash = item.Value.GetProperty("hash").GetString()!;
                    var sub = hash[..2];
                    var dest = Path.Combine(Config.AssetsDir, "objects", sub, hash);
                    if (File.Exists(dest)) return;
                    var url = Config.UseBmcl
                        ? "https://bmclapi2.bangbang93.com/assets/" + sub + "/" + hash
                        : "https://resources.download.minecraft.net/" + sub + "/" + hash;
                    Net.DownloadIfMissing(url, dest);
                    var n = Interlocked.Increment(ref done);
                    if (n % 50 == 0)
                        stage($"下载游戏资源 {n}/{objects.Count}", 0.65 + 0.33 * n / Math.Max(1, objects.Count));
                });
            }
            catch { }
        }

        stage($"{id} 安装完成", 1.0);
    }

    private static string MirrorLibrary(string url) =>
        Config.UseBmcl && url.Contains("libraries.minecraft.net")
            ? url.Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
            : url;

    private static string MirrorAsset(string url) =>
        Config.UseBmcl && url.Contains("launchermeta.mojang.com")
            ? url.Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
            : url;

    private static void ExtractNatives(string jarPath, string nativeDir)
    {
        try
        {
            if (!File.Exists(jarPath)) return;
            Directory.CreateDirectory(nativeDir);
            using var zip = ZipFile.OpenRead(jarPath);
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                if (!entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !entry.Name.EndsWith(".so", StringComparison.OrdinalIgnoreCase) &&
                    !entry.Name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)) continue;
                var dest = Path.Combine(nativeDir, entry.Name);
                if (!File.Exists(dest)) entry.ExtractToFile(dest, true);
            }
        }
        catch { }
    }

    public static void Delete(string versionId)
    {
        var dir = Path.Combine(Config.VersionsDir, versionId);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    /// <summary>游戏崩溃报告目录。</summary>
    public static string CrashReportDir(string versionId) => Path.Combine(Config.GameDirFor(versionId), "crash-reports");
}
