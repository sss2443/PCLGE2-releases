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
        BorderThickness = new Thickness(0),
        Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFB)),
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private readonly TextBlock _statusText = new() { Text = "就绪", VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    private readonly ProgressBar _progress = new()
    {
        Height = 6,
        Width = 220,
        Margin = new Thickness(12, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed
    };

    private int _current = -1;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
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
        // ---- 顶栏 ----
        var top = new Border
        {
            Background = new LinearGradientBrush(Color.FromRgb(0x27, 0x3A, 0x4C), Color.FromRgb(0x33, 0x4E, 0x66), 0),
            Padding = new Thickness(14, 8, 14, 8)
        };
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(new TextBlock
        {
            Text = "PCL",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        brand.Children.Add(new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(1, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "GE",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0x3A, 0x4C))
            }
        });
        brand.Children.Add(new TextBlock
        {
            Text = "1.00",
            Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xDA, 0xEE)),
            FontSize = 12,
            Margin = new Thickness(10, 6, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        topGrid.Children.Add(brand);

        var nav = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < _titles.Length; i++)
        {
            var button = new Button
            {
                Content = _titles[i],
                Foreground = new SolidColorBrush(Color.FromRgb(0xCF, 0xE3, 0xF2)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 6, 18, 6),
                Margin = new Thickness(4, 0, 4, 0),
                MinWidth = 64,
                FontSize = 14,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var index = i;
            button.Click += (_, _) => ShowPage(index);
            _navButtons[i] = button;
            nav.Children.Add(button);
        }
        Grid.SetColumn(nav, 2);
        topGrid.Children.Add(nav);
        top.Child = topGrid;
        Grid.SetRow(top, 0);
        RootGrid.Children.Add(top);

        // ---- 页面容器 ----
        var host = new Grid();
        for (var i = 0; i < _titles.Length; i++)
        {
            _pages[i] = _pageImpl[i].Build();
            _pages[i].Visibility = Visibility.Collapsed;
            host.Children.Add(_pages[i]);
        }
        Grid.SetRow(host, 1);
        RootGrid.Children.Add(host);

        // ---- 底栏 ----
        var bottom = new StackPanel();
        bottom.Children.Add(_logBox);
        var statusRow = new DockPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF2, 0xF6)),
            Height = 26
        };
        var versionText = new TextBlock
        {
            Text = "PCLGE 1.00 · C# / WPF",
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
            _navButtons[i].Background = visible ? UiKit.Primary : Brushes.Transparent;
            _navButtons[i].Foreground = visible ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xCF, 0xE3, 0xF2));
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
