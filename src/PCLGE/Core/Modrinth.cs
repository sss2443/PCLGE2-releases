using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Web;

namespace Pclge.Core;

/// <summary>Modrinth：搜索与下载模组 / 资源包 / 整合包 / 光影。</summary>
public static class Modrinth
{
    private const string Api = "https://api.modrinth.com/v2";

    public record Hit(string Title, string Slug, string Description, long Downloads, string Author);

    public static List<Hit> Search(string query, string type, string gameVersion, string loader, int limit = 20)
    {
        var facets = new List<string> { "[\"project_type:" + type + "\"]" };
        if (!string.IsNullOrWhiteSpace(gameVersion)) facets.Add("[\"versions:" + gameVersion + "\"]");
        if (!string.IsNullOrWhiteSpace(loader)) facets.Add("[\"categories:" + loader + "\"]");
        var url = $"{Api}/search?limit={limit}&index=relevance&query={HttpUtility.UrlEncode(query ?? "")}"
                  + "&facets=" + HttpUtility.UrlEncode("[" + string.Join(",", facets) + "]");
        var list = new List<Hit>();
        using var doc = JsonDocument.Parse(Net.GetText(url));
        foreach (var e in doc.RootElement.GetProperty("hits").EnumerateArray())
        {
            list.Add(new Hit(
                e.GetProperty("title").GetString()!,
                e.GetProperty("slug").GetString()!,
                e.TryGetProperty("description", out var d) ? d.GetString()! : "",
                e.TryGetProperty("downloads", out var dl) ? dl.GetInt64() : 0,
                e.TryGetProperty("author", out var a) ? a.GetString()! : ""));
        }
        return list;
    }

    private static (string FileName, string Url) LatestFile(string slug, string gameVersion, string loader)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(gameVersion))
            query.Add("game_versions=" + HttpUtility.UrlEncode("[\"" + gameVersion + "\"]"));
        if (!string.IsNullOrWhiteSpace(loader))
            query.Add("loaders=" + HttpUtility.UrlEncode("[\"" + loader + "\"]"));
        var url = $"{Api}/project/{slug}/version" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        using var doc = JsonDocument.Parse(Net.GetText(url));
        var arr = doc.RootElement;
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            throw new Exception($"该项目没有适配 {gameVersion} / {loader} 的版本");
        var file = arr[0].GetProperty("files")[0];
        return (file.GetProperty("filename").GetString()!, file.GetProperty("url").GetString()!);
    }

    /// <summary>下载到指定实例对应目录，返回落盘路径。</summary>
    public static string Install(string slug, string type, string gameVersion, string loader,
        string targetVersionId, Action<string> log)
    {
        var (fileName, downloadUrl) = LatestFile(slug, gameVersion, loader);
        log($"下载 {fileName} ← {downloadUrl}");
        var dest = type switch
        {
            "mod" => Path.Combine(ModManager.ModsDir(targetVersionId), fileName),
            "resourcepack" => Path.Combine(ModManager.ResourcePacksDir(targetVersionId), fileName),
            "shader" => Path.Combine(ModManager.GameDir(targetVersionId), "shaderpacks", fileName),
            _ => Path.Combine(ModManager.GameDir(targetVersionId), fileName)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        Net.Download(downloadUrl, dest);
        log("已保存：" + dest);
        return dest;
    }

    /// <summary>安装整合包：解析 modrinth.index.json，把文件下载到实例目录并解压 overrides。</summary>
    public static void InstallModpack(string slug, string gameVersion, string targetVersionId, Action<string> log)
    {
        var (fileName, url) = LatestFile(slug, gameVersion, null);
        var mrpack = Path.Combine(Config.InstallerDir, fileName);
        log($"下载整合包 {fileName}");
        Net.Download(url, mrpack);

        var gameDir = ModManager.GameDir(targetVersionId);
        Directory.CreateDirectory(gameDir);
        using var zip = ZipFile.OpenRead(mrpack);

        var indexEntry = zip.GetEntry("modrinth.index.json");
        if (indexEntry != null)
        {
            using var reader = new StreamReader(indexEntry.Open());
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("files", out var files))
            {
                var total = files.GetArrayLength();
                var i = 0;
                foreach (var f in files.EnumerateArray())
                {
                    i++;
                    var path = f.GetProperty("path").GetString()!;
                    var dest = Path.Combine(gameDir, path.Replace('/', Path.DirectorySeparatorChar));
                    var downloads = f.GetProperty("downloads");
                    if (downloads.GetArrayLength() == 0) continue;
                    var fileUrl = downloads[0].GetString()!;
                    log($"[{i}/{total}] {path}");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    Net.DownloadIfMissing(fileUrl, dest);
                }
            }
        }

        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase) || entry.FullName.Length <= 10)
                continue;
            var relative = entry.FullName["overrides/".Length..];
            var dest = Path.Combine(gameDir, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (string.IsNullOrEmpty(entry.Name)) continue;
            entry.ExtractToFile(dest, true);
        }
        log("整合包安装完成：" + gameDir);
    }
}
