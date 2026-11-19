using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Pclge.Core;

public record Account(string Name, string Uuid, string Type = "offline");

/// <summary>账户：离线账户列表 + 正版 UUID 计算 + 皮肤文件关联。</summary>
public static class AccountService
{
    public const string DefaultName = "Steve";

    private static List<Account> _cache;

    public static List<Account> All
    {
        get
        {
            if (_cache != null) return _cache;
            _cache = new List<Account>();
            var raw = Config.Get("accounts", "");
            foreach (var item in raw.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split(',');
                var name = parts[0];
                if (!ValidName(name)) continue;
                var uuid = parts.Length > 1 && parts[1].Length == 32 ? parts[1] : OfflineUuid(name);
                _cache.Add(new Account(name, uuid));
            }
            if (_cache.Count == 0)
            {
                var name = Config.PlayerName;
                if (!ValidName(name)) name = DefaultName;
                _cache.Add(new Account(name, OfflineUuid(name)));
                Persist();
            }
            return _cache;
        }
    }

    private static void Persist()
    {
        Config.Put("accounts", string.Join("|", _cache.Select(a => a.Name + "," + a.Uuid.Replace("-", ""))));
    }

    public static string CurrentName
    {
        get
        {
            var name = Config.Get("playerName", "");
            if (ValidName(name)) return name;
            return All[0].Name;
        }
    }

    public static void Add(string name)
    {
        if (!ValidName(name)) throw new Exception("账户名只能字母、数字、下划线，长度 1-16");
        var list = All;
        if (list.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            SetCurrent(name);
            return;
        }
        list.Add(new Account(name, OfflineUuid(name)));
        Persist();
        SetCurrent(name);
    }

    public static void Remove(string name)
    {
        if (All.Count <= 1) throw new Exception("至少保留一个账户");
        _cache.RemoveAll(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        Persist();
        if (string.Equals(CurrentName, name, StringComparison.OrdinalIgnoreCase)) SetCurrent(_cache[0].Name);
    }

    public static void SetCurrent(string name)
    {
        if (ValidName(name)) Config.Put("playerName", name);
    }

    public static Account Get(string name) =>
        All.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? new Account(name, OfflineUuid(name));

    public static bool ValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && Regex.IsMatch(name, "^[A-Za-z0-9_]{1,16}$");

    /// <summary>离线账户 UUID（与 Minecraft 一致的离线 UUID v3）。</summary>
    public static string OfflineUuid(string name)
    {
        var bytes = Encoding.UTF8.GetBytes("OfflinePlayer:" + name);
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(bytes);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}
