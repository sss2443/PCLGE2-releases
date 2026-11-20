using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Pclge.Core;
using Pclge.Ui;

namespace Pclge.Views;

/// <summary>联机页：局域网 UDP 广播聊天 + 在线玩家列表。</summary>
public sealed class LinkPage : IPage
{
    private readonly MainWindow _shell;
    private readonly LanChat _chat = new();

    private readonly TextBox _nick = UiKit.Input("", 130);
    private readonly TextBox _port = UiKit.Input(LanChat.DefaultPort.ToString(), 80);
    private readonly TextBox _history = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        Height = 380,
        FontFamily = UiKit.Mono,
        FontSize = 12,
        Background = Brushes.White,
        Foreground = UiKit.TextDark,
        BorderBrush = UiKit.Line,
        BorderThickness = new Thickness(1),
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(8)
    };
    private readonly TextBox _input = new() { Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(6) };
    private readonly ListBox _peers = UiKit.List(300);
    private readonly TextBlock _state = UiKit.Label("未启动", UiKit.Muted);
    private readonly Button _toggle = UiKit.PrimaryButton("启动联机", (_, _) => { });
    private System.Threading.Timer _timer;

    public LinkPage(MainWindow shell)
    {
        _shell = shell;
        _shell.LanChatInstance = _chat;
        _toggle.Click += (_, _) => Toggle();
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Send();
                e.Handled = true;
            }
        };
        _chat.OnChat += (nick, message, self) =>
        {
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                var time = DateTime.Now.ToString("HH:mm:ss");
                _history.AppendText($"[{time}] {(self ? "我" : nick)}：{message}{Environment.NewLine}");
                _history.ScrollToEnd();
            }));
        };
        _chat.OnPeersChanged += () =>
        {
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                _peers.ItemsSource = new ObservableCollection<string>(_chat.Peers);
            }));
        };
        _chat.OnSystem += message => _shell.Dispatcher.BeginInvoke(new Action(() => _shell.Status(message)));
    }

    public string Title => "联机";

    public Grid Build()
    {
        var left = new StackPanel();
        left.Children.Add(UiKit.Heading("联机"));
        left.Children.Add(UiKit.Note("UDP " + LanChat.DefaultPort + " 局域网广播"));
        left.Children.Add(UiKit.Label("在线玩家"));
        left.Children.Add(_peers);
        left.Children.Add(UiKit.SeparatorLine());
        left.Children.Add(UiKit.Note("同一局域网内的 PCLGE 会自动互相发现；异地联机可以用 EasyTier / ZeroTier 组一个虚拟局域网，再在这里聊天。"));
        left.Children.Add(UiKit.Button("打开 EasyTier 官网", (_, _) => MainWindow.OpenUrl("https://easytier.cn/")));

        var right = new StackPanel();
        right.Children.Add(UiKit.Card("房间",
            UiKit.Row(UiKit.Label("昵称"), _nick, UiKit.Label("端口"), _port, _toggle),
            UiKit.Row(UiKit.Label("状态"), _state),
            UiKit.Note("双方端口要一致；如果 Windows 防火墙弹窗，请允许专用网络。")));
        right.Children.Add(UiKit.Card("聊天",
            _history,
            _input,
            UiKit.Row(UiKit.Button("发送（回车）", (_, _) => Send()),
                UiKit.Button("清空记录", (_, _) => _history.Clear()))));
        return UiKit.TwoPane(left, right);
    }

    public void Refresh()
    {
        if (_nick.Text.Length == 0) _nick.Text = AccountService.CurrentName;
        _state.Text = _chat.Running ? $"已启动 · 端口 {_chat.Port}" : "未启动";
        _state.Foreground = _chat.Running ? UiKit.Ok : UiKit.Muted;
        _toggle.Content = _chat.Running ? "停止联机" : "启动联机";
        _peers.ItemsSource = new ObservableCollection<string>(_chat.Peers);
    }

    private void Toggle()
    {
        if (_chat.Running)
        {
            _timer?.Dispose();
            _timer = null;
            _chat.Stop();
            Refresh();
            _shell.Status("联机已停止");
            return;
        }
        if (!int.TryParse(_port.Text.Trim(), out var port) || port < 1 || port > 65535) port = LanChat.DefaultPort;
        try
        {
            _chat.Start(string.IsNullOrWhiteSpace(_nick.Text) ? AccountService.CurrentName : _nick.Text.Trim(), port);
            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => { if (_chat.Running) _chat.Heartbeat(); }, null, 20000, 20000);
            Refresh();
        }
        catch (Exception ex)
        {
            _shell.Status("启动失败：" + ex.Message + "（端口可能被占用）");
        }
    }

    private void Send()
    {
        var message = _input.Text.Trim();
        if (message.Length == 0) return;
        if (!_chat.Running)
        {
            _shell.Status("先点「启动联机」");
            return;
        }
        _chat.Send(message);
        _input.Clear();
    }
}
