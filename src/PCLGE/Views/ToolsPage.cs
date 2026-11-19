using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Pclge.Core;
using Pclge.Ui;

namespace Pclge.Views;

/// <summary>工具页：皮肤 / 成就图 / 崩溃分析 / 日志 / 帮助。</summary>
public sealed class ToolsPage : IPage
{
    private readonly MainWindow _shell;

    private readonly TextBox _lsUser = UiKit.Input("", 170);
    private readonly TextBox _skinAccount = UiKit.Input("", 150);
    private readonly TextBlock _skinStatus = UiKit.Note("");
    private readonly Image _skinPreview = new()
    {
        Width = 96,
        Height = 96,
        Margin = new Thickness(0, 4, 12, 4),
        Stretch = System.Windows.Media.Stretch.Uniform
    };

    private readonly TextBox _achIcon = UiKit.Input("钻石", 120);
    private readonly TextBox _achTitle = UiKit.Input("钻石！", 160);
    private readonly TextBox _achDesc = UiKit.Input("挖到钻石", 220);
    private readonly TextBox _achDesc2 = UiKit.Input("", 220);
    private readonly Image _achPreview = new() { Width = 320, Height = 64, Margin = new Thickness(0, 6, 0, 6) };

    private readonly ListBox _crashList = UiKit.List(320);
    private readonly TextBox _crashDetail = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        FontFamily = UiKit.Mono,
        Height = 130,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private readonly TextBox _logView = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        FontFamily = UiKit.Mono,
        Height = 400,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    public ToolsPage(MainWindow shell) => _shell = shell;

    public string Title => "工具";

    public Grid Build()
    {
        var panes = new List<UIElement>
        {
            BuildSkinPane(),
            BuildAchievementPane(),
            BuildCrashPane(),
            BuildLogPane(),
            BuildHelpPane()
        };
        var nav = UiKit.List(170);
        var host = UiKit.SubPager(panes, nav, "皮肤", "成就图", "崩溃分析", "日志", "帮助");

        var left = new StackPanel();
        left.Children.Add(UiKit.Heading("工具"));
        left.Children.Add(nav);
        left.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
        left.Children.Add(UiKit.Button("打开皮肤目录", (_, _) => MainWindow.OpenFolder(Config.SkinsDir)));
        left.Children.Add(UiKit.Button("打开日志目录", (_, _) => MainWindow.OpenFolder(Config.LogDir)));

        return UiKit.TwoPane(left, host);
    }

    private UIElement BuildSkinPane()
    {
        var panel = new StackPanel();
        panel.Children.Add(UiKit.Card("LittleSkin 下载",
            UiKit.Row(_lsUser, UiKit.PrimaryButton("从 LittleSkin 下载皮肤", (_, _) => DownloadSkin())),
            UiKit.Note("输入 LittleSkin 的角色名，皮肤会保存到本地 skins 目录。"),
            UiKit.Row(_skinPreview, _skinStatus)));

        panel.Children.Add(UiKit.Card("本地皮肤",
            UiKit.Row(UiKit.Label("账户"), _skinAccount),
            UiKit.Row(
                UiKit.PrimaryButton("导入 PNG 皮肤", (_, _) => ImportSkin()),
                UiKit.Button("应用到当前实例", (_, _) => ApplySkin()),
                UiKit.Button("预览", (_, _) => PreviewSkin()))));

        panel.Children.Add(UiKit.Card("说明",
            UiKit.Note("离线账户的皮肤只会保存在启动器与实例目录；要让游戏内生效需要安装离线皮肤模组。"),
            UiKit.Note("皮肤图片需要 64×64（1.8+）或 64×32（旧版）PNG。")));
        return UiKit.Scroll(panel);
    }

    private UIElement BuildAchievementPane()
    {
        var panel = new StackPanel();
        panel.Children.Add(UiKit.Card("成就图生成",
            UiKit.Row(UiKit.Label("图标文字"), _achIcon),
            UiKit.Row(UiKit.Label("成就名"), _achTitle),
            UiKit.Row(UiKit.Label("描述一"), _achDesc),
            UiKit.Row(UiKit.Label("描述二"), _achDesc2),
            UiKit.Row(
                UiKit.PrimaryButton("生成并保存 PNG", (_, _) => GenerateAchievement()),
                UiKit.Button("只预览", (_, _) => PreviewAchievement())),
            _achPreview,
            UiKit.Note("输出为 320×64 的原版成就弹窗风格图片，可用作视频封面或截图素材。")));
        return UiKit.Scroll(panel);
    }

    private UIElement BuildCrashPane()
    {
        var panel = new StackPanel();
        panel.Children.Add(UiKit.Card("崩溃分析",
            UiKit.Note("读取 crash-reports 与 logs/latest.log，自动给出可能原因和处理办法。"),
            UiKit.Row(
                UiKit.PrimaryButton("分析当前实例", (_, _) => AnalyzeCrash()),
                UiKit.Button("导出崩溃报告到桌面", (_, _) => ExportCrash()),
                UiKit.Button("打开崩溃报告目录", (_, _) => OpenCrashDir())),
            _crashList,
            _crashDetail));
        _crashList.SelectionChanged += (_, _) =>
        {
            if (_crashList.SelectedItem is FindingView view) _crashDetail.Text = view.Detail;
        };
        return UiKit.Scroll(panel);
    }

    private UIElement BuildLogPane()
    {
        var panel = new StackPanel();
        panel.Children.Add(UiKit.Card("运行日志",
            UiKit.Row(
                UiKit.Button("刷新", (_, _) => RefreshLog()),
                UiKit.Button("清空", (_, _) =>
                {
                    LogService.Clear();
                    _logView.Clear();
                }),
                UiKit.Button("打开日志文件夹", (_, _) => MainWindow.OpenFolder(Config.LogDir)),
                UiKit.Button("打开游戏日志", (_, _) => OpenGameLog())),
            _logView));
        return UiKit.Scroll(panel);
    }

    private UIElement BuildHelpPane()
    {
        var panel = new StackPanel();
        panel.Children.Add(UiKit.Card("常见问题",
            UiKit.Label("· 启动失败：先看「崩溃分析」，再确认「设置 → Java」版本是否匹配。"),
            UiKit.Label("· 下载很慢：在「设置 → 下载」切到 BMCLAPI 镜像。"),
            UiKit.Label("· 模组不生效：确认加载器版本与模组支持的游戏版本一致。"),
            UiKit.Label("· 联机看不到对方：确认在同一局域网 / EasyTier 网络，防火墙放行 UDP 25567。"),
            UiKit.Label("· 皮肤不显示：离线皮肤需要模组支持，或使用正版服务器。"),
            UiKit.Label("· 存档找不到：开了版本隔离时存档在 versions/<版本>/.minecraft/saves。")));
        panel.Children.Add(UiKit.Card("快捷键与目录",
            UiKit.Note("配置文件：" + Config.ConfigFile),
            UiKit.Note("游戏目录：" + Config.GameDir),
            UiKit.Note("日志目录：" + Config.LogDir)));
        return UiKit.Scroll(panel);
    }

    private record FindingView(string Title, string Advice, string Detail)
    {
        public override string ToString() => Title;
    }

    // ================= 刷新 =================
    public void Refresh()
    {
        var account = AccountService.CurrentName;
        if (_skinAccount.Text.Length == 0) _skinAccount.Text = account;
        RefreshLog();
        if (VersionService.Installed().Count > 0) _crashList.ItemsSource ??= new List<FindingView>();
    }

    private void RefreshLog()
    {
        _logView.Text = string.Join(Environment.NewLine, LogService.Snapshot());
        _logView.ScrollToEnd();
    }

    // ================= 皮肤 =================
    private void DownloadSkin()
    {
        var user = _lsUser.Text.Trim();
        if (!AccountService.ValidName(user))
        {
            _skinStatus.Text = "角色名只能字母、数字、下划线，1-16 位";
            return;
        }
        _skinStatus.Text = "正在下载...";
        _shell.RunAsync(() =>
        {
            var path = SkinService.DownloadLittleSkin(user, s => _shell.Log(s));
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                _skinStatus.Text = "已下载：" + path;
                _skinStatus.Foreground = UiKit.Ok;
                _shell.Status("皮肤已下载：" + user);
                ShowSkin(user);
            }));
        });
    }

    private void ImportSkin()
    {
        var dialog = new OpenFileDialog { Title = "选择皮肤 PNG", Filter = "PNG 图片|*.png" };
        if (dialog.ShowDialog() != true) return;
        var target = _skinAccount.Text.Trim();
        if (!AccountService.ValidName(target))
        {
            _skinStatus.Text = "先填账户名";
            return;
        }
        try
        {
            var path = SkinService.ImportSkin(dialog.FileName, target);
            _skinStatus.Text = "已导入：" + path;
            ShowSkin(target);
        }
        catch (Exception ex)
        {
            _skinStatus.Text = "导入失败：" + ex.Message;
        }
    }

    private void PreviewSkin() => ShowSkin(_skinAccount.Text.Trim());

    private void ShowSkin(string account)
    {
        try
        {
            var path = SkinService.SkinFile(account);
            if (!File.Exists(path))
            {
                _skinStatus.Text = "还没有 " + account + " 的皮肤文件";
                return;
            }
            var bitmap = new BitmapImage();
            using var stream = File.OpenRead(path);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            _skinPreview.Source = bitmap;
            _skinStatus.Text = "当前皮肤：" + path;
            _skinStatus.Foreground = UiKit.Ok;
        }
        catch (Exception ex)
        {
            _skinStatus.Text = "预览失败：" + ex.Message;
        }
    }

    private void ApplySkin()
    {
        var account = _skinAccount.Text.Trim();
        var version = VersionService.Installed().FirstOrDefault();
        if (version == null)
        {
            _skinStatus.Text = "还没有安装游戏版本";
            return;
        }
        try
        {
            var dest = SkinService.ApplyToInstance(account, version);
            _skinStatus.Text = "已应用：" + dest;
        }
        catch (Exception ex)
        {
            _skinStatus.Text = "应用失败：" + ex.Message;
        }
    }

    // ================= 成就图 =================
    private void GenerateAchievement()
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存成就图",
            Filter = "PNG 图片|*.png",
            FileName = "PCLGE-成就图.png",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var path = AchievementImage.Render(_achTitle.Text, _achDesc.Text, _achDesc2.Text, _achIcon.Text, dialog.FileName);
            _shell.Status("成就图已保存：" + path);
            PreviewAchievement();
        }
        catch (Exception ex)
        {
            _shell.Status("生成失败：" + ex.Message);
        }
    }

    private void PreviewAchievement()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pclge-achievement-preview.png");
        try
        {
            AchievementImage.Render(_achTitle.Text, _achDesc.Text, _achDesc2.Text, _achIcon.Text, temp);
            var bitmap = new BitmapImage();
            using var stream = File.OpenRead(temp);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            _achPreview.Source = bitmap;
        }
        catch (Exception ex)
        {
            _shell.Status("预览失败：" + ex.Message);
        }
    }

    // ================= 崩溃分析 =================
    private string CurrentVersion =>
        VersionService.Installed().FirstOrDefault() ?? "";

    private void AnalyzeCrash()
    {
        var version = CurrentVersion;
        if (version.Length == 0)
        {
            _shell.Status("还没有安装游戏版本");
            return;
        }
        _shell.RunAsync(() =>
        {
            var findings = CrashAnalyzer.Analyze(version);
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                _crashList.ItemsSource = findings
                    .Select(f => new FindingView(f.Title, f.Advice, $"【{f.Title}】{Environment.NewLine}{f.Advice}{Environment.NewLine}{Environment.NewLine}{f.Detail}"))
                    .ToList();
                _crashList.SelectedIndex = 0;
                _shell.Status($"分析完成：{version}，{findings.Count} 条结论");
            }));
        });
    }

    private void ExportCrash()
    {
        var version = CurrentVersion;
        if (version.Length == 0) return;
        try
        {
            var path = CrashAnalyzer.ExportLatest(version);
            _shell.Status(path == null ? "该实例没有崩溃报告" : "已导出：" + path);
        }
        catch (Exception ex)
        {
            _shell.Status("导出失败：" + ex.Message);
        }
    }

    private void OpenCrashDir()
    {
        var version = CurrentVersion;
        if (version.Length == 0) return;
        MainWindow.OpenFolder(VersionService.CrashReportDir(version));
    }

    private void OpenGameLog()
    {
        var version = CurrentVersion;
        if (version.Length == 0) return;
        var log = Path.Combine(ModManager.GameDir(version), "logs", "latest.log");
        if (!File.Exists(log))
        {
            _shell.Status("还没有游戏日志");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + log + "\"") { UseShellExecute = true });
        }
        catch
        {
            MainWindow.OpenFolder(Path.GetDirectoryName(log)!);
        }
    }
}
