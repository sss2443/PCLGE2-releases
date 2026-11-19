using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Pclge.Core;

/// <summary>启动游戏：拼装 JVM / 游戏参数并拉起 Java 进程。</summary>
public static class GameLauncher
{
    public static Process Current { get; private set; }

    public static bool IsRunning => Current is { HasExited: false };

    public record LaunchOptions(string VersionId, string PlayerName);

    public static int Launch(string versionId, string playerName, int memoryMb, List<string> extraJvmArgs,
        Action<string> log)
    {
        var versionDir = Path.Combine(Config.VersionsDir, versionId);
        var jsonPath = Path.Combine(versionDir, versionId + ".json");
        if (!File.Exists(jsonPath)) throw new Exception("版本不存在：" + versionId);

        var docs = Libraries.Chain(versionId);
        if (docs.Count == 0) throw new Exception("无法读取版本信息：" + versionId);

        JsonElement root = docs[0].RootElement;
        var mainClass = root.TryGetProperty("mainClass", out var mc) ? mc.GetString() : null;
        if (string.IsNullOrEmpty(mainClass)) throw new Exception("版本信息缺少 mainClass");

        var gameDir = Config.GameDirFor(versionId);
        Directory.CreateDirectory(gameDir);
        var natives = Path.Combine(versionDir, "natives");
        Directory.CreateDirectory(natives);

        // ---- classpath ----
        var classpath = new List<string>();
        foreach (var doc in docs)
        {
            if (!doc.RootElement.TryGetProperty("libraries", out var libs)) continue;
            foreach (var lib in libs.EnumerateArray())
            {
                if (!Libraries.Allowed(lib)) continue;
                var artifact = Libraries.Resolve(lib);
                if (artifact == null) continue;
                var full = Path.Combine(Config.LibrariesDir, artifact.Value.Path);
                if (File.Exists(full) && !classpath.Contains(full)) classpath.Add(full);
                // 新版系统的 native 库（lwjgl natives）同样进 classpath
                var native = Libraries.ResolveNative(lib);
                if (native != null)
                {
                    var nativePath = Path.Combine(Config.LibrariesDir, native.Value.Path);
                    if (File.Exists(nativePath) && !classpath.Contains(nativePath)) classpath.Add(nativePath);
                }
            }
        }
        var clientJar = Path.Combine(versionDir, versionId + ".jar");
        if (!File.Exists(clientJar))
        {
            var parent = root.TryGetProperty("inheritsFrom", out var p) ? p.GetString() : null;
            if (!string.IsNullOrEmpty(parent))
            {
                var parentJar = Path.Combine(Config.VersionsDir, parent, parent + ".jar");
                if (File.Exists(parentJar)) clientJar = parentJar;
            }
        }
        if (File.Exists(clientJar)) classpath.Add(clientJar);

        // ---- 资源索引 ----
        string assetIndexId = "legacy";
        foreach (var doc in docs)
        {
            if (doc.RootElement.TryGetProperty("assetIndex", out var ai) &&
                ai.TryGetProperty("id", out var aid))
            {
                assetIndexId = aid.GetString()!;
                break;
            }
        }

        // ---- 占位符 ----
        var account = AccountService.Get(playerName);
        var replacements = new Dictionary<string, string>
        {
            ["${auth_player_name}"] = playerName,
            ["${version_name}"] = versionId,
            ["${game_directory}"] = gameDir,
            ["${assets_root}"] = Config.AssetsDir,
            ["${game_assets}"] = Path.Combine(Config.AssetsDir, "virtual", "legacy"),
            ["${assets_index_name}"] = assetIndexId,
            ["${auth_uuid}"] = account.Uuid.Replace("-", ""),
            ["${auth_access_token}"] = "0",
            ["${auth_session}"] = "-",
            ["${clientid}"] = "pclge",
            ["${auth_xuid}"] = "0",
            ["${user_type}"] = "legacy",
            ["${version_type}"] = "PCLGE",
            ["${user_properties}"] = "{}",
            ["${natives_directory}"] = natives,
            ["${launcher_name}"] = "PCLGE",
            ["${launcher_version}"] = "1.00",
            ["${classpath}"] = string.Join(";", classpath),
            ["${classpath_separator}"] = ";",
            ["${library_directory}"] = Config.LibrariesDir
        };

        string Expand(string text)
        {
            foreach (var kv in replacements) text = text.Replace(kv.Key, kv.Value);
            return text;
        }

        // ---- JVM 参数 ----
        var jvm = new List<string> { $"-Xmx{memoryMb}M", $"-Xms{Math.Min(memoryMb, 1024)}M" };
        var foundClassPath = false;
        foreach (var doc in docs)
        {
            if (!doc.RootElement.TryGetProperty("arguments", out var args) ||
                !args.TryGetProperty("jvm", out var jvmArgs)) continue;
            foreach (var item in jvmArgs.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var text = item.GetString()!;
                    if (text.StartsWith("-Xmx") || text.StartsWith("-Xms")) continue;
                    if (text == "-cp" || text == "-classpath") foundClassPath = true;
                    jvm.Add(Expand(text));
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    if (!RuleMatch(item)) continue;
                    if (!item.TryGetProperty("value", out var value)) continue;
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        var text = Expand(value.GetString()!);
                        if (text.StartsWith("-Xmx") || text.StartsWith("-Xms")) continue;
                        if (text == "-cp" || text == "-classpath") foundClassPath = true;
                        jvm.Add(text);
                    }
                    else if (value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in value.EnumerateArray()) jvm.Add(Expand(v.GetString()!));
                    }
                }
            }
        }
        jvm.Add("-Dminecraft.launcher.brand=PCLGE");
        jvm.Add("-Dminecraft.launcher.version=1.00");
        if (!Directory.Exists(Path.Combine(Config.AssetsDir, "virtual", "legacy")))
            Directory.CreateDirectory(Path.Combine(Config.AssetsDir, "virtual", "legacy"));
        if (extraJvmArgs is { Count: > 0 }) jvm.AddRange(extraJvmArgs);
        if (!foundClassPath) jvm.AddRange(new[] { "-cp", string.Join(";", classpath) });
        jvm.Add(mainClass);

        // ---- 游戏参数 ----
        var gameArgs = new List<string>();
        bool got = false;
        foreach (var doc in docs)
        {
            if (!doc.RootElement.TryGetProperty("arguments", out var args) ||
                !args.TryGetProperty("game", out var gameList)) continue;
            foreach (var item in gameList.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    gameArgs.Add(Expand(item.GetString()!));
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    if (!RuleMatch(item)) continue;
                    if (!item.TryGetProperty("value", out var value)) continue;
                    if (value.ValueKind == JsonValueKind.String) gameArgs.Add(Expand(value.GetString()!));
                    else if (value.ValueKind == JsonValueKind.Array)
                        foreach (var v in value.EnumerateArray()) gameArgs.Add(Expand(v.GetString()!));
                }
            }
            got = true;
            break;
        }
        if (!got)
        {
            var legacy = docs.Select(d => d.RootElement.TryGetProperty("minecraftArguments", out var ma) ? ma.GetString() : null)
                .FirstOrDefault(s => !string.IsNullOrEmpty(s));
            if (legacy != null)
                foreach (var part in legacy.Split(' ', StringSplitOptions.RemoveEmptyEntries)) gameArgs.Add(Expand(part));
        }
        if (gameArgs.Count == 0)
        {
            gameArgs.AddRange(new[]
            {
                "--username", playerName, "--version", versionId, "--gameDir", gameDir,
                "--assetsDir", Config.AssetsDir, "--assetIndex", assetIndexId,
                "--uuid", account.Uuid.Replace("-", ""), "--accessToken", "0",
                "--userType", "legacy", "--versionType", "PCLGE"
            });
        }

        var javaExe = JavaService.ResolveFor(versionId);
        log("Java：" + javaExe);
        log("游戏目录：" + gameDir);
        log($"内存：{memoryMb}MB，参数共 {jvm.Count + gameArgs.Count} 项");

        var psi = new ProcessStartInfo(javaExe)
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = gameDir
        };
        foreach (var a in jvm) psi.ArgumentList.Add(a);
        foreach (var a in gameArgs) psi.ArgumentList.Add(a);

        var proc = Process.Start(psi) ?? throw new Exception("Java 进程启动失败");
        Current = proc;
        log("游戏进程已启动，PID " + proc.Id);
        proc.WaitForExit();
        log("游戏已退出，退出码 " + proc.ExitCode);
        Current = null;
        return proc.ExitCode;
    }

    /// <summary>arguments 里带 rules 的条目是否匹配当前平台。</summary>
    private static bool RuleMatch(JsonElement item)
    {
        if (!item.TryGetProperty("rules", out var rules)) return true;
        bool allow = false;
        foreach (var rule in rules.EnumerateArray())
        {
            var action = rule.TryGetProperty("action", out var a) ? a.GetString() : "allow";
            var match = true;
            if (rule.TryGetProperty("os", out var os))
            {
                if (os.TryGetProperty("name", out var name)) match &= name.GetString() == "windows";
                if (os.TryGetProperty("arch", out var arch))
                    match &= arch.GetString() == "x86" ? !Environment.Is64BitOperatingSystem : Environment.Is64BitOperatingSystem;
            }
            if (rule.TryGetProperty("features", out var features))
            {
                if (features.TryGetProperty("is_demo_user", out var demo) && demo.GetBoolean()) match = false;
                if (features.TryGetProperty("has_custom_resolution", out var res) && res.GetBoolean()) match = false;
            }
            if (match) allow = action == "allow";
        }
        return allow;
    }
}
