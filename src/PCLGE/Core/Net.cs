using System.IO;
using System.Net.Http;

namespace Pclge.Core;

/// <summary>HTTP 工具：文本、下载（带进度、失败保留临时文件、重试一次）。</summary>
public static class Net
{
    public static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

    static Net()
    {
        Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "PCLGE/1.00 (Minecraft Launcher; +https://github.com/sss2443/PCLGE2releases)");
    }

    public static string GetText(string url) => Http.GetStringAsync(url).GetAwaiter().GetResult();

    /// <summary>下载文件，progress 回调 (已下载字节, 总字节或 -1)。</summary>
    public static void Download(string url, string dest, Action<long, long> progress = null)
    {
        var folder = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        var tmp = dest + ".tmp";

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var resp = Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                    .GetAwaiter().GetResult();
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1;
                using var source = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using var target = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[65536];
                long done = 0;
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    target.Write(buffer, 0, read);
                    done += read;
                    progress?.Invoke(done, total);
                }
                target.Flush();
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(tmp, dest);
                return;
            }
            catch when (attempt == 0)
            {
                Thread.Sleep(200);
            }
        }
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
    }

    /// <summary>对象存储（assets）下载：已存在则跳过，单文件失败不阻断整体安装。</summary>
    public static bool DownloadIfMissing(string url, string dest)
    {
        if (File.Exists(dest) && new FileInfo(dest).Length > 0) return true;
        try
        {
            Download(url, dest);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
