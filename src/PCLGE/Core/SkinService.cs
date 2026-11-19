using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Pclge.Core;

/// <summary>皮肤：LittleSkin 下载、本地导入、自定义头像/皮肤。</summary>
public static class SkinService
{
    public static string LittleSkinUuid(string userName)
    {
        var body = "[\"" + userName + "\"]";
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var resp = Net.Http.PostAsync("https://littleskin.cn/api/yggdrasil/api/profiles/minecraft", content)
            .GetAwaiter().GetResult();
        resp.EnsureSuccessStatusCode();
        var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(text);
        var arr = doc.RootElement;
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            throw new Exception("LittleSkin 上找不到角色：" + userName);
        return arr[0].GetProperty("id").GetString()!;
    }

    public static string LittleSkinSkinUrl(string uuid)
    {
        var body = Net.GetText($"https://littleskin.cn/api/yggdrasil/sessionserver/session/minecraft/profile/{uuid}");
        using var doc = JsonDocument.Parse(body);
        foreach (var prop in doc.RootElement.GetProperty("properties").EnumerateArray())
        {
            if (prop.GetProperty("name").GetString() != "textures") continue;
            var decoded = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(prop.GetProperty("value").GetString()!));
            using var tex = JsonDocument.Parse(decoded);
            if (!tex.RootElement.TryGetProperty("textures", out var textures)) break;
            if (!textures.TryGetProperty("SKIN", out var skin)) break;
            return skin.GetProperty("url").GetString()!;
        }
        throw new Exception("该角色没有上传皮肤");
    }

    public static string DownloadLittleSkin(string userName, Action<string> log = null)
    {
        if (!AccountService.ValidName(userName)) throw new Exception("用户名只能字母、数字、下划线，1-16 位");
        Directory.CreateDirectory(Config.SkinsDir);
        log?.Invoke("查询 LittleSkin 角色：" + userName);
        var uuid = LittleSkinUuid(userName);
        var url = LittleSkinSkinUrl(uuid);
        log?.Invoke("皮肤地址：" + url);
        var dest = Path.Combine(Config.SkinsDir, userName + ".png");
        Net.Download(url, dest);
        return dest;
    }

    public static string ImportSkin(string sourceImage, string targetName)
    {
        if (!AccountService.ValidName(targetName)) throw new Exception("账户名只能字母、数字、下划线，1-16 位");
        var ext = Path.GetExtension(sourceImage).ToLowerInvariant();
        if (ext != ".png") throw new Exception("皮肤需要 PNG 格式（64×64 或 64×32）");
        Directory.CreateDirectory(Config.SkinsDir);
        var dest = Path.Combine(Config.SkinsDir, targetName + ".png");
        File.Copy(sourceImage, dest, true);
        return dest;
    }

    public static string SkinFile(string accountName) => Path.Combine(Config.SkinsDir, accountName + ".png");

    public static bool HasSkin(string accountName) => File.Exists(SkinFile(accountName));

    /// <summary>把皮肤复制进实例目录，便于部分离线皮肤模组读取。</summary>
    public static string ApplyToInstance(string accountName, string versionId)
    {
        var source = SkinFile(accountName);
        if (!File.Exists(source)) throw new Exception("还没有 " + accountName + " 的皮肤文件");
        var dir = Path.Combine(ModManager.GameDir(versionId), "PCLGE", "skins");
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, accountName + ".png");
        File.Copy(source, dest, true);
        return dest;
    }
}
