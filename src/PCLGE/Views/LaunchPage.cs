using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Pclge.Core;
using Pclge.Ui;

namespace Pclge.Views;

/// <summary>启动页：版本列表 + 离线账户 + 内存 / Java + 启动。</summary>
public sealed class LaunchPage : IPage
{
    private readonly MainWindow _shell;
    private readonly ListBox _versions = UiKit.List();
    private readonly TextBox _search = UiKit.Input("", 150);
    private readonly ComboBox _accounts = UiKit.Combo(180);
    private readonly TextBlock _versionInfo = UiKit.Label("未选择版本", UiKit.Muted);
    private readonly TextBlock _javaText = UiKit.Label("检测中...", UiKit.Muted);
    private readonly TextBox _memory = UiKit.Input(Config.MemoryMb.ToString(), 80);
    private readonly TextBox _jvm = UiKit.Input(Config.JvmArgs);
    private readonly CheckBox _isolation = new()
    {
        Content = "版本隔离（每个版本独立存档与 mods）",
        IsChecked = Config.Isolation,
        Margin = new Thickness(0, 4, 0, 4),
        FontFamily = UiKit.Font,
        FontSize = 13
    };
    private readonly Button _launchButton = UiKit.PrimaryButton("启动游戏", (_, _) => { });
    private readonly Button _killButton = UiKit.Button("结束游戏", (_, _) => { });
    private string _filter = "";

    public LaunchPage(MainWindow shell)
    {
        _shell = shell;
        _launchButton.Click += (_, _) => Launch();
        _killButton.Click += (_, _) => Kill();
    }

    public string Title => "启动";

    public Grid Build()
    {
        _search.TextChanged += (_, _) =>
        {
            _filter = _search.Text.Trim();
            RefreshVersions();
        };
        _versions.SelectionChanged += (_, _) => UpdateSelection();
        _accounts.SelectionChanged += (_, _) =>
        {
            var name = _accounts.SelectedItem as string;
            if (name != null) AccountService.SetCurrent(name);
        };
        _isolation.Checked += (_, _) => Config.Put("isolation", "1");
        _isolation.Unchecked += (_, _) => Config.Put("isolation", "0");

        var left = new StackPanel();
        left.Children.Add(UiKit.Heading("版本列表"));
        left.Children.Add(_search);
        left.Children.Add(_versions);
        left.Children.Add(UiKit.Button("刷新", (_, _) => Refresh()));
        left.Children.Add(UiKit.Button("打开游戏目录", (_, _) => MainWindow.OpenFolder(Config.GameDir)));
        left.Children.Add(UiKit.Button("删除选中版本", (_, _) => DeleteVersion()));

        var panel = new StackPanel();
        panel.Children.Add(UiKit.Row(UiKit.Label("版本"), _versionInfo));
        panel.Children.Add(UiKit.Row(UiKit.Label("账户"), _accounts,
            UiKit.Button("新建离线账户", (_, _) => NewAccount()),
            UiKit.Button("删除账户", (_, _) => RemoveAccount())));
        panel.Children.Add(UiKit.Row(UiKit.Label("内存 (MB)"), _memory));
        panel.Children.Add(UiKit.Row(UiKit.Label("Java"), _javaText, UiKit.Button("重新检测", (_, _) =>
        {
            JavaService.Scan(true);
            UpdateJava();
        })));
        panel.Children.Add(UiKit.Row(UiKit.Label("额外 JVM 参数"), _jvm));
        panel.Children.Add(_isolation);
        panel.Children.Add(UiKit.Row(_launchButton, _killButton));

        var right = new StackPanel();
        right.Children.Add(UiKit.Card("启动游戏", panel,
            UiKit.Note("离线账户仅用于单人游戏；进服务器需要正版账号的服务器支持。")));
        right.Children.Add(UiKit.Card("公告",
            UiKit.Label("PCLGE 1.00 —— 我的世界启动器（C# / WPF）"),
            UiKit.Label("· 原版游戏下载，支持 Fabric / Quilt / Forge / NeoForge"),
            UiKit.Label("· Modrinth 模组、资源包、光影、整合包"),
            UiKit.Label("· LittleSkin 皮肤下载与自定义皮肤"),
            UiKit.Label("· 局域网 UDP 联机聊天（端口 " + LanChat.DefaultPort + "）")));
        right.Children.Add(UiKit.Card("提示",
            UiKit.Note("首次使用请先到「下载」页安装游戏版本；启动失败时去「工具 → 崩溃分析」看原因。"),
            UiKit.Button("打开 GitHub 仓库", (_, _) => MainWindow.OpenUrl("https://github.com/sss2443/PCLGE2-releases"))));
        return UiKit.TwoPane(left, right);
    }

    public void Refresh()
    {
        RefreshVersions();
        UpdateJava();
        var names = new ObservableCollection<string>(AccountService.All.Select(a => a.Name));
        var current = AccountService.CurrentName;
        _accounts.ItemsSource = names;
        _accounts.SelectedItem = names.FirstOrDefault(n => n == current) ?? names.FirstOrDefault();
        _memory.Text = Config.MemoryMb.ToString();
        _jvm.Text = Config.JvmArgs;
    }

    private void RefreshVersions()
    {
        var all = VersionService.Installed();
        var filtered = _filter.Length == 0
            ? all
            : all.Where(v => v.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToList();
        var selected = _versions.SelectedItem as string;
        _versions.ItemsSource = filtered;
        if (selected != null && filtered.Contains(selected)) _versions.SelectedItem = selected;
        else if (filtered.Count > 0) _versions.SelectedIndex = 0;
        if (filtered.Count == 0) _versionInfo.Text = "还没有安装版本，去「下载」页安装";
    }

    private void UpdateSelection()
    {
        if (_versions.SelectedItem is not string id) return;
        var detail = VersionService.Inspect(id);
        _versionInfo.Text = $"{id}（{detail.Loader} / {detail.Type}，{detail.Size / 1048576.0:F1} MB）";
    }

    private void UpdateJava()
    {
        var list = JavaService.Scan();
        var chosen = JavaService.Resolve();
        _javaText.Text = list.Count == 0
            ? "没有检测到 Java，请到「设置 → Java」手动指定"
            : $"{Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(chosen)))} · {chosen}";
    }

    private void Launch()
    {
        if (_versions.SelectedItem is not string version)
        {
            _shell.Status("先在左侧选一个版本");
            return;
        }
        if (GameLauncher.IsRunning)
        {
            _shell.Status("游戏已经在运行");
            return;
        }
        var name = AccountService.CurrentName;
        if (!AccountService.ValidName(name))
        {
            _shell.Status("账户名不合法");
            return;
        }
        if (!int.TryParse(_memory.Text.Trim(), out var memory) || memory < 256) memory = 2048;
        if (memory > 32768) memory = 32768;
        Config.Put("memory", memory.ToString());
        Config.Put("jvmArgs", _jvm.Text.Trim());
        Config.Put("playerName", name);

        _shell.Status($"启动 {version}（{name}）");
        _shell.SetProgress(0.1, "启动中...");
        _shell.RunAsync(() =>
        {
            var extra = _jvm.Text.Trim().Length > 0
                ? _jvm.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();
            var code = GameLauncher.Launch(version, name, memory, extra, s => _shell.Log(s));
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                _shell.SetProgress(0);
                _shell.Status(code == 0 ? "游戏已正常退出" : $"游戏异常退出，代码 {code}（去「工具 → 崩溃分析」查看原因）");
            }));
        });
    }

    private void Kill()
    {
        if (!GameLauncher.IsRunning)
        {
            _shell.Status("没有正在运行的游戏");
            return;
        }
        try
        {
            GameLauncher.Current?.Kill(true);
            _shell.Status("已结束游戏进程");
        }
        catch (Exception ex)
        {
            _shell.Status("结束失败：" + ex.Message);
        }
    }

    private void NewAccount()
    {
        var name = Prompt("新建离线账户", "账户名（字母/数字/下划线，1-16 位）", "Steve");
        if (name == null) return;
        try
        {
            AccountService.Add(name.Trim());
            Refresh();
            _shell.Status("已添加账户：" + name.Trim());
        }
        catch (Exception ex)
        {
            _shell.Status(ex.Message);
        }
    }

    private void RemoveAccount()
    {
        if (_accounts.SelectedItem is not string name) return;
        if (MessageBox.Show($"删除账户 {name}？存档不会受影响。", "PCLGE",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try
        {
            AccountService.Remove(name);
            Refresh();
            _shell.Status("已删除账户：" + name);
        }
        catch (Exception ex)
        {
            _shell.Status(ex.Message);
        }
    }

    private void DeleteVersion()
    {
        if (_versions.SelectedItem is not string id) return;
        if (MessageBox.Show($"删除版本 {id}？该版本的 mods 与存档会一起删除（版本隔离时）。", "PCLGE",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try
        {
            VersionService.Delete(id);
            Refresh();
            _shell.Status("已删除版本：" + id);
        }
        catch (Exception ex)
        {
            _shell.Status("删除失败：" + ex.Message);
        }
    }

    /// <summary>简单的单行输入对话框。</summary>
    internal static string Prompt(string title, string label, string initial)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = UiKit.PageBg,
            FontFamily = UiKit.Font,
            FontSize = 13
        };
        var stack = new StackPanel { Margin = new Thickness(18) };
        stack.Children.Add(UiKit.Label(label));
        var box = UiKit.Input(initial);
        stack.Children.Add(box);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = UiKit.PrimaryButton("确定", (_, _) => { dialog.DialogResult = true; });
        var cancel = UiKit.Button("取消", (_, _) => { dialog.DialogResult = false; });
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        stack.Children.Add(buttons);
        dialog.Content = stack;
        dialog.Owner = Application.Current.MainWindow;
        dialog.Loaded += (_, _) => box.Focus();
        return dialog.ShowDialog() == true ? box.Text : null;
    }
}
