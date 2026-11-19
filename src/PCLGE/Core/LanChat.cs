using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Pclge.Core;

/// <summary>局域网 UDP 广播聊天，协议 [CHAT]昵称||消息 / [PING]昵称 / [BYE]昵称，端口 25567。</summary>
public sealed class LanChat : IDisposable
{
    public const int DefaultPort = 25567;

    private UdpClient _client;
    private string _nick = "玩家";
    private volatile bool _running;
    private readonly HashSet<string> _peers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private int _port = DefaultPort;

    public event Action<string, string, bool> OnChat;   // 昵称, 消息, 是否本人
    public event Action OnPeersChanged;
    public event Action<string> OnSystem;

    public bool Running => _running;
    public IReadOnlyCollection<string> Peers
    {
        get { lock (_lock) return _peers.ToList(); }
    }

    public int Port => _port;

    public void Start(string nick, int port = DefaultPort)
    {
        if (_running) Stop();
        _nick = string.IsNullOrWhiteSpace(nick) ? "玩家" : nick.Trim();
        _port = port;
        _client = new UdpClient();
        _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _client.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
        _client.EnableBroadcast = true;
        _running = true;
        new Thread(Loop) { IsBackground = true, Name = "pclge-lan-chat" }.Start();
        Broadcast($"[PING]{_nick}");
        OnSystem?.Invoke($"联机已启动，昵称 {_nick}，端口 {_port}");
    }

    private void Loop()
    {
        while (_running)
        {
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                var data = _client.Receive(ref remote);
                var text = Encoding.UTF8.GetString(data);
                if (text.StartsWith("[CHAT]"))
                {
                    var body = text["[CHAT]".Length..];
                    var i = body.IndexOf("||", StringComparison.Ordinal);
                    if (i <= 0) continue;
                    var nick = body[..i];
                    var message = body[(i + 2)..];
                    bool self = string.Equals(nick, _nick, StringComparison.OrdinalIgnoreCase);
                    bool added;
                    lock (_lock) added = _peers.Add(nick);
                    OnChat?.Invoke(nick, message, self);
                    if (added) OnPeersChanged?.Invoke();
                }
                else if (text.StartsWith("[PING]"))
                {
                    var nick = text["[PING]".Length..];
                    if (string.Equals(nick, _nick, StringComparison.OrdinalIgnoreCase)) continue;
                    bool added;
                    lock (_lock) added = _peers.Add(nick);
                    if (added)
                    {
                        OnPeersChanged?.Invoke();
                        Broadcast($"[PING]{_nick}");
                    }
                }
                else if (text.StartsWith("[BYE]"))
                {
                    var nick = text["[BYE]".Length..];
                    lock (_lock) _peers.Remove(nick);
                    OnPeersChanged?.Invoke();
                }
            }
            catch (SocketException)
            {
                if (!_running) return;
                Thread.Sleep(200);
            }
            catch
            {
                Thread.Sleep(200);
            }
        }
    }

    public void Send(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Broadcast($"[CHAT]{_nick}||{message}");
        OnChat?.Invoke(_nick, message, true);
    }

    public void Heartbeat() => Broadcast($"[PING]{_nick}");

    private void Broadcast(string text)
    {
        var client = _client;
        if (client == null) return;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            client.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, _port));
            // 同时向回环发送，方便同一台电脑多开测试
            client.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, _port));
        }
        catch
        {
            // 广播失败不影响本地聊天
        }
    }

    public void Stop()
    {
        if (!_running) return;
        Broadcast($"[BYE]{_nick}");
        _running = false;
        try { _client?.Close(); } catch { }
        _client = null;
        lock (_lock) _peers.Clear();
        OnPeersChanged?.Invoke();
    }

    public void Dispose() => Stop();
}
