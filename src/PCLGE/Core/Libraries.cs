using System.IO;
using System.Text.Json;

namespace Pclge.Core;

/// <summary>版本 JSON 中 libraries / rules 的解析，安装与启动共用。</summary>
public static class Libraries
{
    /// <summary>判断当前平台（Windows）是否允许该库。</summary>
    public static bool Allowed(JsonElement lib)
    {
        if (!lib.TryGetProperty("rules", out var rules)) return true;
        bool allow = false;
        foreach (var rule in rules.EnumerateArray())
        {
            var action = rule.TryGetProperty("action", out var a) ? a.GetString() : "allow";
            var match = true;
            if (rule.TryGetProperty("os", out var os))
            {
                if (os.TryGetProperty("name", out var name) && name.GetString() is { } osName)
                    match &= osName == "windows";
                if (os.TryGetProperty("arch", out var arch) && arch.GetString() is { } archName)
                    match &= archName == "x86" ? !Environment.Is64BitOperatingSystem : Environment.Is64BitOperatingSystem;
                if (os.TryGetProperty("version", out var ver) && ver.GetString() is { } verPattern)
                {
                    try { match &= System.Text.RegularExpressions.Regex.IsMatch(Environment.OSVersion.Version.ToString(), verPattern); }
                    catch { }
                }
            }
            if (rule.TryGetProperty("features", out var features))
            {
                // demo / custom resolution 等特性默认关闭
                if (features.TryGetProperty("is_demo_user", out var demo) && demo.GetBoolean()) match = false;
                if (features.TryGetProperty("has_custom_resolution", out var res) && res.GetBoolean()) match = false;
            }
            if (match) allow = action == "allow";
        }
        return allow;
    }

    /// <summary>库的 Maven 坐标 → 相对路径。</summary>
    public static string RelativePath(string name)
    {
        var parts = name.Split(':');
        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts.Length > 2 ? parts[2] : "";
        var classifier = parts.Length > 3 ? "-" + parts[3] : "";
        var ext = parts.Length > 4 ? parts[4] : "jar";
        return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.{ext}";
    }

    /// <summary>解析出 (相对路径, 下载地址)；无法解析返回 null。</summary>
    public static (string Path, string Url)? Resolve(JsonElement lib)
    {
        try
        {
            if (lib.TryGetProperty("downloads", out var downloads) &&
                downloads.TryGetProperty("artifact", out var artifact) &&
                artifact.TryGetProperty("url", out var urlProp))
            {
                var url = urlProp.GetString();
                var path = artifact.TryGetProperty("path", out var p)
                    ? p.GetString()
                    : RelativePath(lib.GetProperty("name").GetString()!);
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(path)) return null;
                return (path.Replace('/', Path.DirectorySeparatorChar), url);
            }

            if (!lib.TryGetProperty("name", out var nameProp)) return null;
            var name = nameProp.GetString();
            if (string.IsNullOrWhiteSpace(name)) return null;
            var relative = RelativePath(name);
            var baseUrl = lib.TryGetProperty("url", out var u) && u.GetString() is { } bu && bu.Length > 0
                ? bu
                : "https://libraries.minecraft.net/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return (relative.Replace('/', Path.DirectorySeparatorChar), baseUrl + relative);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>老版本（1.17 之前）的原生库分类器，如 natives-windows。</summary>
    public static (string Path, string Url)? ResolveNative(JsonElement lib)
    {
        try
        {
            if (!lib.TryGetProperty("natives", out var natives)) return null;
            if (!natives.TryGetProperty("windows", out var win)) return null;
            var classifier = win.GetString()!.Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32");
            if (lib.TryGetProperty("downloads", out var d) && d.TryGetProperty("classifiers", out var cls) &&
                cls.TryGetProperty(classifier, out var entry))
            {
                return (entry.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar),
                    entry.GetProperty("url").GetString()!);
            }
            if (!lib.TryGetProperty("name", out var nameProp)) return null;
            var name = nameProp.GetString()!;
            var parts = name.Split(':');
            var group = parts[0].Replace('.', '/');
            var artifact = parts[1];
            var version = parts.Length > 2 ? parts[2] : "";
            var rel = $"{group}/{artifact}/{version}/{artifact}-{version}-{classifier}.jar";
            var baseUrl = lib.TryGetProperty("url", out var u) && u.GetString() is { } bu && bu.Length > 0
                ? bu
                : "https://libraries.minecraft.net/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return (rel.Replace('/', Path.DirectorySeparatorChar), baseUrl + rel);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>合并版本继承链（子版本在前）。</summary>
    public static List<JsonDocument> Chain(string versionId)
    {
        var docs = new List<JsonDocument>();
        var current = versionId;
        var guard = 0;
        while (!string.IsNullOrEmpty(current) && guard++ < 8)
        {
            var jsonPath = Path.Combine(Config.VersionsDir, current, current + ".json");
            if (!File.Exists(jsonPath)) break;
            var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            docs.Add(doc);
            current = doc.RootElement.TryGetProperty("inheritsFrom", out var parent)
                ? parent.GetString()
                : null;
        }
        return docs;
    }
}
