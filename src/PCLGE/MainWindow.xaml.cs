using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pclge.Core;
using Pclge.Ui;
using Pclge.Views;

namespace Pclge;

/// <summary>主窗口：顶部导航 + 页面容器 + 底部日志与状态栏。</summary>
public partial class MainWindow : Window
{
    private readonly string[] _titles = { "启动", "基岩版", "下载", "设置", "工具", "联机" };
    private readonly Button[] _navButtons = new Button[6];
    private readonly Grid[] _pages = new Grid[6];
    private readonly IPage[] _pageImpl;

    private readonly TextBox _logBox = new()
    {
        IsReadOnly = true,
        Height = 72,
        FontFamily = UiKit.Mono,
        FontSize = 12,
        Foreground = UiKit.TextDark,
        BorderThickness = new Thickness(0),
        Background = UiKit.SunkenBg,
        Padding = new Thickness(10, 4, 10, 4),
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private readonly TextBlock _statusText = new()
    {
        Text = "就绪",
        VerticalAlignment = VerticalAlignment.Center,
        FontFamily = UiKit.Font,
        FontSize = 12,
        Foreground = UiKit.TextDark
    };
    private readonly ProgressBar _progress = new()
    {
        Height = 5,
        Width = 220,
        Margin = new Thickness(12, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = UiKit.Primary,
        Background = UiKit.Line,
        BorderThickness = new Thickness(0),
        Visibility = Visibility.Collapsed
    };

    private int _current = -1;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        // 界面字体：默认内嵌的 Minecraft 像素字体，可在设置页切回微软雅黑
        UiKit.Font = string.Equals(Config.Get("uiFont", "minecraft"), "yahei", StringComparison.OrdinalIgnoreCase)
            ? UiKit.YaHei
            : UiKit.McFont;
        FontFamily = UiKit.Font;
        Directory.CreateDirectory(Config.LauncherDir);
        _pageImpl = new IPage[]
        {
            new LaunchPage(this),
            new BedrockPage(this),
            new DownloadPage(this),
            new SetupPage(this),
            new ToolsPage(this),
            new LinkPage(this)
        };
        BuildShell();
        LogService.OnLine += OnLogLine;
        foreach (var line in LogService.Snapshot()) _logBox.AppendText(line + Environment.NewLine);
        Log($"PCLGE 1.00 启动，游戏目录：{Config.GameDir}");
        ShowPage(0);
        Closed += (_, _) =>
        {
            if (LanChatInstance != null) LanChatInstance.Dispose();
        };
    }

    public LanChat LanChatInstance { get; set; }

    private void OnLogLine(string line)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _logBox.AppendText(line + Environment.NewLine);
            _logBox.ScrollToEnd();
        }));
    }

    private void BuildShell()
    {
        // ---- 顶栏：白底 + Gray6 下边线，左侧 PCL 字标，右侧六个导航（PCL 的按钮底色取 Gray7，选中取 ColorBrush3）----
        var topContent = new Grid();
        topContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        };
        brand.Children.Add(new TextBlock
        {
            Text = "PCL",
            FontFamily = UiKit.Font,
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = UiKit.TextDark,
            VerticalAlignment = VerticalAlignment.Center
        });
        brand.Children.Add(new Border
        {
            Background = UiKit.Primary,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(2, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "GE",
                FontFamily = UiKit.Font,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            }
        });
        brand.Children.Add(new TextBlock
        {
            Text = "1.00",
            FontFamily = UiKit.Font,
            FontSize = 12,
            Foreground = UiKit.Muted,
            Margin = new Thickness(9, 7, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        topContent.Children.Add(brand);

        var nav = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < _titles.Length; i++)
        {
            var button = new Button
            {
                Content = _titles[i],
                FontFamily = UiKit.Font,
                FontSize = 13,
                Foreground = UiKit.TextDark,
                Background = UiKit.PanelBg,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 6, 18, 6),
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 62,
                Height = 30,
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = NavButtonTemplate()
            };
            var index = i;
            button.Click += (_, _) => ShowPage(index);
            _navButtons[i] = button;
            nav.Children.Add(button);
        }
        Grid.SetColumn(nav, 2);
        topContent.Children.Add(nav);

        var top = new Border
        {
            Background = Brushes.White,
            BorderBrush = UiKit.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 7, 14, 7),
            Child = topContent
        };
        Grid.SetRow(top, 0);
        RootGrid.Children.Add(top);

        // ---- 页面容器 ----
        var host = new Grid { Background = UiKit.PageBg };
        for (var i = 0; i < _titles.Length; i++)
        {
            _pages[i] = _pageImpl[i].Build();
            _pages[i].Visibility = Visibility.Collapsed;
            host.Children.Add(_pages[i]);
        }
        Grid.SetRow(host, 1);
        RootGrid.Children.Add(host);

        // ---- 底栏：Gray8 日志区 + Gray7 状态栏 ----
        var bottom = new StackPanel();
        var logBorder = new Border
        {
            Background = UiKit.SunkenBg,
            BorderBrush = UiKit.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _logBox
        };
        bottom.Children.Add(logBorder);

        var statusRow = new DockPanel { Background = UiKit.PanelBg, Height = 26 };
        var versionText = new TextBlock
        {
            Text = "PCLGE 1.00 · C# / WPF · 界面取自 PCL CE",
            FontFamily = UiKit.Font,
            FontSize = 12,
            Foreground = UiKit.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 10, 0)
        };
        DockPanel.SetDock(versionText, Dock.Right);
        statusRow.Children.Add(versionText);
        var statusLeft = new StackPanel { Orientation = Orientation.Horizontal };
        statusLeft.Children.Add(new TextBlock
        {
            Text = "状态：",
            FontFamily = UiKit.Font,
            FontSize = 12,
            Foreground = UiKit.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        });
        statusLeft.Children.Add(_statusText);
        statusLeft.Children.Add(_progress);
        statusRow.Children.Add(statusLeft);
        bottom.Children.Add(statusRow);
        Grid.SetRow(bottom, 2);
        RootGrid.Children.Add(bottom);
    }

    /// <summary>导航按钮模板：圆角 3；未选中 Gray7 底 + 悬停 Gray6，选中 ColorBrush3 底 + 悬停 ColorBrush4。</summary>
    private static ControlTemplate NavButtonTemplate(bool selected = false)
    {
        var template = new ControlTemplate(typeof(Button));
        var root = new FrameworkElementFactory(typeof(Border), "PART_Bg");
        root.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        root.SetValue(Border.BackgroundProperty, selected ? UiKit.Primary : UiKit.PanelBg);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(presenter);
        template.VisualTree = root;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty,
            selected ? UiKit.PrimaryHover : UiKit.Line, "PART_Bg"));
        template.Triggers.Add(hover);
        return template;
    }

    private void ShowPage(int index)
    {
        if (index < 0 || index >= _pages.Length) return;
        if (_current == index && _current >= 0) return;
        for (var i = 0; i < _pages.Length; i++)
        {
            var visible = i == index;
            _pages[i].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            _pages[i].IsHitTestVisible = visible;
            if (visible) Panel.SetZIndex(_pages[i], 2);
            _navButtons[i].Template = NavButtonTemplate(visible);
            _navButtons[i].Foreground = visible ? Brushes.White : UiKit.TextDark;
        }
        _current = index;
        try { _pageImpl[index].Refresh(); }
        catch (Exception ex) { Log("页面刷新失败：" + ex.Message); }
    }

    public void RefreshCurrent()
    {
        if (_current >= 0) _pageImpl[_current].Refresh();
    }

    // ================= 对外服务 =================
    public void Log(string text) => LogService.Write(text);

    public void Status(string text)
    {
        _statusText.Text = text;
        Log(text);
    }

    public void SetProgress(double value, string text = null)
    {
        _progress.Visibility = value > 0 ? Visibility.Visible : Visibility.Collapsed;
        _progress.Value = Math.Clamp(value * 100, 0, 100);
        if (text != null) _statusText.Text = text;
    }

    /// <summary>后台线程执行，异常统一提示，避免界面卡死。</summary>
    public void RunAsync(Action work)
    {
        if (_busy)
        {
            Status("上一个任务还没结束，请稍等");
            return;
        }
        _busy = true;
        new Thread(() =>
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetProgress(0);
                    _statusText.Text = "出错：" + ex.Message;
                    Log("出错：" + ex.Message);
                }));
            }
            finally
            {
                _busy = false;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_progress.Value >= 100 || _progress.Value == 0) SetProgress(0);
                    RefreshCurrent();
                }));
            }
        })
        { IsBackground = true }.Start();
    }

    public bool Busy => _busy;

    public static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }
        catch { }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }
}
