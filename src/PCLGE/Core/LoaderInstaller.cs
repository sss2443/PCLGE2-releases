using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Pclge.Core;

/// <summary>Fabric / Quilt / Forge / NeoForge / OptiFine 安装。</summary>
public static class LoaderInstaller
{
    public enum Kind { Fabric, Quilt, Forge, NeoForge }

    public static List<string> FabricLoaders(string mc)
    {
        var body = Net.GetText($"https://meta.fabricmc.net/v2/versions/loader/{mc}");
        var list = new List<string>();
        foreach (var e in JsonDocument.Parse(body).RootElement.EnumerateArray())
            if (e.TryGetProperty("loader", out var l) && l.TryGetProperty("version", out var v))
                list.Add(v.GetString()!);
        if (list.Count == 0) throw new Exception($"Fabric 没有适配 {mc} 的加载器");
        return list;
    }

    public static List<string> QuiltLoaders(string mc)
    {
        var body = Net.GetText($"https://meta.quiltmc.org/v3/versions/loader/{mc}");
        var list = new List<string>();
        foreach (var e in JsonDocument.Parse(body).RootElement.EnumerateArray())
            if (e.TryGetProperty("loader", out var l) && l.TryGetProperty("version", out var v))
                list.Add(v.GetString()!);
        if (list.Count == 0) throw new Exception($"Quilt 没有适配 {mc} 的加载器");
        return list;
    }

    /// <summary>从 Maven 目录列表里取可用版本（Forge / NeoForge）。</summary>
    public static List<string> ForgeVersions(string mc)
    {
        var body = Net.GetText("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
        var list = new List<string>();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("promos", out var promos))
            foreach (var p in promos.EnumerateObject())
            {
                if (!p.Name.StartsWith(mc + "-")) continue;
                var v = p.Value.GetString()!;
                if (!v.Contains("-") && !list.Contains(v)) list.Add(v);
            }
        if (list.Count == 0)
            throw new Exception($"Forge 没有找到 {mc} 的推荐版本，可手动填写加载器版本");
        return list;
    }

    public static string InstallFabric(string mc, string loader)
    {
        var id = $"{mc}-fabric-{loader}";
        var url = $"https://meta.fabricmc.net/v2/versions/loader/{mc}/{loader}/profile/json";
        var json = Net.GetText(url);
        var dir = Path.Combine(Config.VersionsDir, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, id + ".json"), json);
        return id;
    }

    public static string InstallQuilt(string mc, string loader)
    {
        var id = $"{mc}-quilt-{loader}";
        var url = $"https://meta.quiltmc.org/v3/versions/loader/{mc}/{loader}/profile/json";
        var json = Net.GetText(url);
        var dir = Path.Combine(Config.VersionsDir, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, id + ".json"), json);
        return id;
    }

    /// <summary>Forge / NeoForge：下载 installer 并执行 --installClient。</summary>
    public static string InstallInstallerBased(Kind kind, string mc, string loaderVersion, Action<string> log)
    {
        string url, fileName, resultId;
        if (kind == Kind.Forge)
        {
            fileName = $"forge-{mc}-{loaderVersion}-installer.jar";
            url = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mc}-{loaderVersion}/{fileName}";
            resultId = $"{mc}-forge-{loaderVersion}";
        }
        else
        {
            fileName = $"neoforge-{loaderVersion}-installer.jar";
            url = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{loaderVersion}/{fileName}";
            resultId = $"neoforge-{loaderVersion}";
        }

        Directory.CreateDirectory(Config.InstallerDir);
        var installer = Path.Combine(Config.InstallerDir, fileName);
        log("下载安装器：" + url);
        Net.Download(url, installer);

        log("运行安装器（可能需要几分钟）...");
        var java = JavaService.ResolveFor(mc);
        var psi = new ProcessStartInfo(java, $"-jar \"{installer}\" --installClient \"{Config.GameDir}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log("[安装器] " + e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log("[安装器] " + e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();
        if (proc.ExitCode != 0) throw new Exception($"{kind} 安装器退出码 {proc.ExitCode}");
        log($"{kind} 安装完成：{resultId}");
        return resultId;
    }

    /// <summary>OptiFine：下载并执行安装器（--installClient 需要图形界面，失败时给出提示）。</summary>
    public static string InstallOptiFine(string mc, string optifineVersion, Action<string> log)
    {
        var url = $"https://bmclapi2.bangbang93.com/optifine/{mc}/{optifineVersion}";
        var file = Path.Combine(Config.InstallerDir, $"OptiFine_{mc}_{optifineVersion}.jar");
        Directory.CreateDirectory(Config.InstallerDir);
        log("下载 OptiFine：" + url);
        Net.Download(url, file);
        var java = JavaService.ResolveFor(mc);
        var psi = new ProcessStartInfo(java, $"-jar \"{file}\" --installClient \"{Config.GameDir}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log("[OptiFine] " + e.Data); };
        proc.BeginOutputReadLine();
        proc.WaitForExit();
        if (proc.ExitCode != 0) throw new Exception("OptiFine 安装器退出码 " + proc.ExitCode);
        return $"{mc}-OptiFine_{optifineVersion}";
    }
}
