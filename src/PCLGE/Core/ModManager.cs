using System.IO;
using System.IO.Compression;

namespace Pclge.Core;

/// <summary>模组 / 资源包 / 存档管理（每个版本一个实例目录）。</summary>
public static class ModManager
{
    public record ModInfo(string FileName, bool Enabled, long Size);

    public static string GameDir(string versionId) => Config.GameDirFor(versionId);

    public static string ModsDir(string versionId) => Path.Combine(GameDir(versionId), "mods");
    public static string ResourcePacksDir(string versionId) => Path.Combine(GameDir(versionId), "resourcepacks");
    public static string SavesDir(string versionId) => Path.Combine(GameDir(versionId), "saves");

    public static List<ModInfo> ListMods(string versionId)
    {
        var list = new List<ModInfo>();
        var dir = ModsDir(versionId);
        if (!Directory.Exists(dir)) return list;
        foreach (var path in Directory.GetFiles(dir))
        {
            var name = Path.GetFileName(path);
            if (!name.EndsWith(".jar") && !name.EndsWith(".jar.disabled")) continue;
            list.Add(new ModInfo(name, !name.EndsWith(".disabled"), new FileInfo(path).Length));
        }
        list.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static void Toggle(string versionId, string fileName)
    {
        var dir = ModsDir(versionId);
        var src = Path.Combine(dir, fileName);
        if (!File.Exists(src)) throw new FileNotFoundException("模组不存在：" + fileName);
        var dst = fileName.EndsWith(".disabled")
            ? Path.Combine(dir, fileName[..^".disabled".Length])
            : Path.Combine(dir, fileName + ".disabled");
        File.Move(src, dst, true);
    }

    public static void Delete(string versionId, string fileName)
    {
        var path = Path.Combine(ModsDir(versionId), fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    public static string Add(string versionId, string jarPath)
    {
        var dir = ModsDir(versionId);
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, Path.GetFileName(jarPath));
        File.Copy(jarPath, dest, true);
        return dest;
    }

    public static string AddResourcePack(string versionId, string zipPath)
    {
        var dir = ResourcePacksDir(versionId);
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, Path.GetFileName(zipPath));
        File.Copy(zipPath, dest, true);
        return dest;
    }

    public static void DeleteResourcePack(string versionId, string fileName)
    {
        var path = Path.Combine(ResourcePacksDir(versionId), fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    public static List<string> ListResourcePacks(string versionId)
    {
        var dir = ResourcePacksDir(versionId);
        if (!Directory.Exists(dir)) return new List<string>();
        var list = Directory.GetFiles(dir, "*.zip").Select(Path.GetFileName).Where(n => n != null).ToList();
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list!;
    }

    public static List<string> ListSaves(string versionId)
    {
        var dir = SavesDir(versionId);
        if (!Directory.Exists(dir)) return new List<string>();
        var list = Directory.GetDirectories(dir).Select(Path.GetFileName).Where(n => n != null).ToList();
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list!;
    }

    public static void DeleteSave(string versionId, string name)
    {
        var dir = Path.Combine(SavesDir(versionId), name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    /// <summary>把存档备份成 zip，返回备份文件路径。</summary>
    public static string BackupSaves(string versionId, string saveName)
    {
        var source = Path.Combine(SavesDir(versionId), saveName);
        if (!Directory.Exists(source)) throw new Exception("存档不存在：" + saveName);
        var backupDir = Path.Combine(Config.LauncherDir, "backups");
        Directory.CreateDirectory(backupDir);
        var dest = Path.Combine(backupDir, $"{saveName}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        ZipFile.CreateFromDirectory(source, dest, CompressionLevel.Optimal, false);
        return dest;
    }

    /// <summary>把旧目录的存档复制到当前实例，返回复制的数量。</summary>
    public static int ImportSaves(string versionId, string sourceSavesDir)
    {
        var target = SavesDir(versionId);
        Directory.CreateDirectory(target);
        var count = 0;
        foreach (var dir in Directory.GetDirectories(sourceSavesDir))
        {
            var dest = Path.Combine(target, Path.GetFileName(dir));
            if (Directory.Exists(dest)) continue;
            CopyTree(dir, dest);
            count++;
        }
        return count;
    }

    public static void CopyTree(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyTree(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
