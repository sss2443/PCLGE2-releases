using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Pclge.Core;
using Pclge.Ui;

namespace Pclge.Views;

/// <summary>基岩版页：BDS 安装、目录关联、服务端启停与 server.properties 编辑。</summary>
public sealed class BedrockPage : IPage
{
    private readonly MainWindow _shell;
    private readonly TextBlock _dirText = UiKit.Label("未关联", UiKit.Muted);
    private readonly TextBlock _stateText = UiKit.Label("未运行", UiKit.Muted);
    private readonly TextBox _port = UiKit.Input("19132", 80);
    private readonly TextBox _props = new()
    {
        AcceptsReturn = true,
        FontFamily = UiKit.Mono,
        Height = 260,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Margin = new Thickness(0, 4, 0, 4)
    };

    public BedrockPage(MainWindow shell) => _shell = shell;

    public string Title => "基岩版";

    public Grid Build()
    {
        var install = new StackPanel();
        install.Children.Add(UiKit.Note("基岩版服务端（BDS）只能运行在 Windows / Linux 服务器上，启动器负责下载、关联与启停。"));
        install.Children.Add(UiKit.Row(
            UiKit.PrimaryButton("下载并安装 BDS", (_, _) => DownloadBds()),
            UiKit.Button("打开官网下载页", (_, _) => MainWindow.OpenUrl("https://www.minecraft.net/en-us/download/server/bedrock"))));
        install.Children.Add(UiKit.Row(
            UiKit.Label("安装目录"), _dirText,
            UiKit.Button("选择已有目录", (_, _) => ChooseDir())));

        var manage = new StackPanel();
        manage.Children.Add(UiKit.Row(UiKit.Label("服务端状态"), _stateText));
        manage.Children.Add(UiKit.Row(
            UiKit.PrimaryButton("启动服务端", (_, _) => StartServer()),
            UiKit.Button("停止服务端", (_, _) =>
            {
                BedrockService.Stop();
                Refresh();
                _shell.Status("已停止基岩版服务端");
            }),
            UiKit.Button("打开目录", (_, _) => MainWindow.OpenFolder(BedrockService.AssociatedDir))));
        manage.Children.Add(UiKit.Row(UiKit.Label("端口（UDP）"), _port,
            UiKit.Button("写入配置", (_, _) => SavePort())));
        manage.Children.Add(UiKit.Heading("server.properties"));
        manage.Children.Add(_props);
        manage.Children.Add(UiKit.Row(
            UiKit.Button("读取", (_, _) => LoadProps()),
            UiKit.PrimaryButton("保存", (_, _) => SaveProps())));

        var help = new StackPanel();
        help.Children.Add(UiKit.Label("联机说明"));
        help.Children.Add(UiKit.Note("· 手机 / 主机版玩家在「服务器」里添加电脑的局域网 IP，端口默认 19132。"));
        help.Children.Add(UiKit.Note("· Windows 第一次运行会弹出防火墙提示，需要允许专用网络。"));
        help.Children.Add(UiKit.Note("· 外网联机需要路由器端口映射，或使用 EasyTier / 内网穿透。"));
        help.Children.Add(UiKit.Note("· 修改 server.properties 后需要重启服务端才生效。"));

        var tabs = new TabControl { Background = Brushes.White, Padding = new Thickness(4) };
        tabs.Items.Add(new TabItem { Header = "安装", Content = UiKit.Scroll(install) });
        tabs.Items.Add(new TabItem { Header = "管理", Content = UiKit.Scroll(manage) });
        tabs.Items.Add(new TabItem { Header = "帮助", Content = UiKit.Scroll(help) });

        var left = new StackPanel();
        left.Children.Add(UiKit.Heading("基岩版"));
        left.Children.Add(UiKit.Note("BDS = Bedrock Dedicated Server"));
        left.Children.Add(UiKit.Label("· 只支持正版玩家进入"));
        left.Children.Add(UiKit.Label("· 版本需与客户端一致"));
        left.Children.Add(UiKit.Label("· 世界文件在 worlds 目录"));

        return UiKit.TwoPane(left, tabs);
    }

    public void Refresh()
    {
        var dir = BedrockService.AssociatedDir;
        _dirText.Text = dir.Length > 0 ? dir : "未关联";
        _stateText.Text = BedrockService.IsRunning ? "运行中，PID " + BedrockService.Current?.Id : "未运行";
        _stateText.Foreground = BedrockService.IsRunning ? UiKit.Ok : UiKit.Muted;
        if (BedrockService.PropertiesPath != null && System.IO.File.Exists(BedrockService.PropertiesPath))
        {
            var props = BedrockService.ReadProperties();
            var match = System.Text.RegularExpressions.Regex.Match(props, @"server-port=(\d+)");
            if (match.Success) _port.Text = match.Groups[1].Value;
            if (!_props.IsFocused) _props.Text = props;
        }
    }

    private void DownloadBds()
    {
        var dialog = new OpenFolderDialog { Title = "选择 BDS 安装目录（建议空目录）" };
        if (dialog.ShowDialog() != true) return;
        _shell.Status("开始下载基岩版服务端");
        _shell.RunAsync(() =>
        {
            BedrockService.Download(dialog.FolderName, s => _shell.Log(s));
            _shell.Dispatcher.BeginInvoke(new Action(() => _shell.Status("基岩版服务端安装完成")));
        });
    }

    private void ChooseDir()
    {
        var dialog = new OpenFolderDialog { Title = "选择 bedrock_server 所在目录" };
        if (dialog.ShowDialog() != true) return;
        Config.Put("bedrockDir", dialog.FolderName);
        Refresh();
        _shell.Status("已关联 BDS 目录：" + dialog.FolderName);
    }

    private void StartServer()
    {
        try
        {
            BedrockService.Start(s => _shell.Log(s));
            Refresh();
            _shell.Status("基岩版服务端已启动");
        }
        catch (Exception ex)
        {
            _shell.Status("启动失败：" + ex.Message);
        }
    }

    private void SavePort()
    {
        try
        {
            var port = _port.Text.Trim();
            if (!int.TryParse(port, out var p) || p < 1 || p > 65535) throw new Exception("端口需要 1-65535");
            var props = BedrockService.ReadProperties();
            if (props.Length == 0) props = "server-port=19132\n";
            props = System.Text.RegularExpressions.Regex.Replace(props, @"server-port=\d+", "server-port=" + port);
            BedrockService.WriteProperties(props);
            _props.Text = props;
            _shell.Status("端口已写入 server.properties：" + port);
        }
        catch (Exception ex)
        {
            _shell.Status("写入失败：" + ex.Message);
        }
    }

    private void LoadProps()
    {
        try
        {
            _props.Text = BedrockService.ReadProperties();
        }
        catch (Exception ex)
        {
            _shell.Status("读取失败：" + ex.Message);
        }
    }

    private void SaveProps()
    {
        try
        {
            BedrockService.WriteProperties(_props.Text);
            _shell.Status("server.properties 已保存");
        }
        catch (Exception ex)
        {
            _shell.Status("保存失败：" + ex.Message);
        }
    }
}
