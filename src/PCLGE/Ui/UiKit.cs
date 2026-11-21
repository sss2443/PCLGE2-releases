using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Pclge.Ui;

/// <summary>页面接口：每个一级导航实现一个。</summary>
public interface IPage
{
    string Title { get; }
    Grid Build();
    void Refresh();
}

/// <summary>
///     界面零件。配色 / 字号 / 圆角 / 间距全部取自 PCL CE 的实现：
///     PCL.Core/UI/Theme/ToneProfile.cs（明色基调）、ThemeService.GetCurrentThemeArgs()（CatBlue 主题）、
///     PCL.Launcher/UI/Styles/PageStyles.axaml（字号、圆角、内边距、卡片阴影）。
///     色值按 OKLCH → sRGB 换算得到，不是估的。
/// </summary>
public static class UiKit
{
    // ---- 主题色（CatBlue，明色）----
    public static readonly Brush Primary = Brush("#2078DA");      // ColorBrush3：主按钮、强调
    public static readonly Brush PrimaryDark = Brush("#1662B6");   // ColorBrush2：深一档强调
    public static readonly Brush PrimaryHover = Brush("#5291DF");  // ColorBrush4：悬停
    public static readonly Brush PrimaryLight = Brush("#9EC1EE");  // ColorBrush5
    public static readonly Brush PrimarySoft = Brush("#D8E6F8");   // ColorBrush6
    public static readonly Brush SelectionBg = Brush("#E1ECFA");   // ColorBrush7：选中行
    public static readonly Brush HoverBg = Brush("#EBF2FC");       // ColorBrush8：悬停行

    // ---- 灰度（ToneProfile 明色）----
    public static readonly Brush TextDark = Brush("#323B47");      // ColorBrush1：正文
    public static readonly Brush Muted = Brush("#797979");         // Gray3：次要文字
    public static readonly Brush Faint = Brush("#8F8F8F");         // Gray4
    public static readonly Brush Line = Brush("#E4E4E4");          // Gray6：描边
    public static readonly Brush PanelBg = Brush("#EBEBEB");       // Gray7：标题栏 / 状态栏
    public static readonly Brush SunkenBg = Brush("#F2F2F2");      // Gray8：日志区

    public static readonly Brush PageBg = Brush("#FDFDFD");        // Background
    public static readonly Brush CardBg = Brushes.White;
    public static readonly Brush HalfWhite = Brush("#55FFFFFF");    // 半透明白（按钮底）
    public static readonly Brush Ok = Brush("#27AE60");
    public static readonly Brush Warn = Brush("#F39C12");          // ColorBrushWarn
    public static readonly Brush Danger = Brush("#E74C3C");         // ColorBrushError

    public static readonly FontFamily YaHei = new global::System.Windows.Media.FontFamily("Microsoft YaHei UI");
    /// <summary>Minecraft_Next_Font_12px2.0（随程序内嵌，缺少的中文字形自动回退到雅黑）。</summary>
    public static readonly FontFamily McFont = LoadMinecraftFont();

    /// <summary>加载内嵌的 Minecraft 字体；资源缺失（例如只拉源码没放 ttf）时回退雅黑。</summary>
    private static FontFamily LoadMinecraftFont()
    {
        try
        {
            var res = new Uri("pack://application:,,,/Assets/Fonts/MinecraftNext.ttf");
            if (global::System.Windows.Application.GetResourceStream(res) == null) return YaHei;
            return new global::System.Windows.Media.FontFamily(new Uri("pack://application:,,,/"),
                "./Assets/Fonts/MinecraftNext.ttf#Minecraft Next Font");
        }
        catch
        {
            return YaHei;
        }
    }

    /// <summary>当前界面字体，启动时按配置设置。</summary>
    public static global::System.Windows.Media.FontFamily Font { get; set; } = YaHei;
    public static readonly FontFamily Mono = new("Consolas");

    // ---- 度量（PageStyles.axaml）----
    private const double CardRadius = 5;     // Border.Card CornerRadius 5
    private const double ButtonRadius = 4;   // Button CornerRadius 4
    private const double ItemRadius = 6;     // Border.PageListItem CornerRadius 6
    private const double BodySize = 13;      // TextBlock FontSize 13
    private const double HintSize = 12;      // Hint / SectionTitle 12

    private static Brush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    // ================= 文本 =================

    /// <summary>页面标题：20 号 SemiBold，下方留 14（TextBlock.PageTitle）。</summary>
    public static TextBlock Title(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeights.SemiBold,
        Foreground = TextDark,
        FontFamily = Font,
        Margin = new Thickness(0, 0, 0, 14),
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    /// <summary>小节标题：12 号、60% 不透明度（TextBlock.SectionTitle）。</summary>
    public static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = HintSize,
        Opacity = 0.6,
        Foreground = TextDark,
        FontFamily = Font,
        Margin = new Thickness(13, 18, 5, 4)
    };

    /// <summary>卡片标题：沿用旧 API 名，视觉改回 PCL 的 14 号加粗。</summary>
    public static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Foreground = TextDark,
        FontFamily = Font,
        Margin = new Thickness(0, 0, 0, 8)
    };

    public static TextBlock Label(string text, Brush brush = null) => new()
    {
        Text = text,
        FontSize = BodySize,
        Foreground = brush ?? TextDark,
        FontFamily = Font,
        Margin = new Thickness(0, 3, 8, 3),
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap
    };

    /// <summary>提示文字：12 号、Gray3（TextBlock.Hint）。</summary>
    public static TextBlock Note(string text) => new()
    {
        Text = text,
        FontSize = HintSize,
        Foreground = Muted,
        FontFamily = Font,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 6)
    };

    // ================= 容器 =================

    public static Border Card(params UIElement[] children) => Card(null, children);

    /// <summary>卡片：圆角 5、白底、0 0 3 的浅阴影（Border.Card）。</summary>
    public static Border Card(string title, params UIElement[] children)
    {
        var stack = new StackPanel();
        if (!string.IsNullOrEmpty(title)) stack.Children.Add(Heading(title));
        foreach (var child in children) stack.Children.Add(child);
        return new Border
        {
            Background = CardBg,
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(13, 11, 13, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 3,
                ShadowDepth = 0,
                Opacity = 0.07,
                RenderingBias = RenderingBias.Performance
            },
            Child = stack
        };
    }

    public static StackPanel Row(params UIElement[] items)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        foreach (var item in items) panel.Children.Add(item);
        return panel;
    }

    public static StackPanel Column(params UIElement[] items)
    {
        var panel = new StackPanel();
        foreach (var item in items) panel.Children.Add(item);
        return panel;
    }

    public static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = new Thickness(0)
    };

    /// <summary>左列表 + 右内容。左栏 260，与 PCL 的 MyPageLeft 一致。</summary>
    public static Grid TwoPane(UIElement left, UIElement right)
    {
        var grid = new Grid { Background = PageBg };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftBorder = new Border
        {
            Background = CardBg,
            CornerRadius = new CornerRadius(CardRadius),
            Padding = new Thickness(8, 10, 8, 10),
            Margin = new Thickness(10, 10, 0, 10),
            Child = Scroll(left)
        };
        var rightBorder = new Border
        {
            Background = PageBg,
            Padding = new Thickness(10),
            Child = Scroll(right)
        };
        Grid.SetColumn(rightBorder, 1);
        grid.Children.Add(leftBorder);
        grid.Children.Add(rightBorder);
        return grid;
    }

    // ================= 按钮 =================

    /// <summary>主按钮：圆角 4、ColorBrush3 底、白字、悬停 ColorBrush4。</summary>
    public static Button PrimaryButton(string text, RoutedEventHandler click)
    {
        var button = FlatButton(Primary, PrimaryHover, null);
        button.FontWeight = FontWeights.SemiBold;
        button.Content = text;
        button.Click += click;
        return button;
    }

    /// <summary>次按钮：半透明白底 + 1 像素 ColorBrush1 描边（MyButton 的默认形态）。</summary>
    public static Button Button(string text, RoutedEventHandler click)
    {
        var button = FlatButton(HalfWhite, HoverBg, TextDark);
        button.Content = text;
        button.Click += click;
        return button;
    }

    private static Button FlatButton(Brush background, Brush hover, Brush border)
    {
        var template = new ControlTemplate(typeof(Button));
        var root = new FrameworkElementFactory(typeof(Border), "PART_Border");
        root.SetValue(Border.BackgroundProperty, background);
        root.SetValue(Border.CornerRadiusProperty, new CornerRadius(ButtonRadius));
        root.SetValue(Border.BorderThicknessProperty, new Thickness(border == null ? 0 : 1));
        root.SetValue(Border.BorderBrushProperty, border ?? Brushes.Transparent);
        root.SetValue(Border.PaddingProperty, new Thickness(14, 6, 14, 6));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(presenter);
        template.VisualTree = root;
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hover, "PART_Border"));
        template.Triggers.Add(hoverTrigger);

        return new Button
        {
            Template = template,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 8, 4),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            FontFamily = Font,
            FontSize = BodySize,
            Foreground = border == null ? Brushes.White : TextDark
        };
    }

    // ================= 输入 =================

    public static TextBox Input(string text, double width = double.NaN)
    {
        var box = new TextBox
        {
            Text = text,
            FontFamily = Font,
            FontSize = BodySize,
            Foreground = TextDark,
            Background = Brushes.White,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 3, 6, 3),
            Margin = new Thickness(0, 3, 8, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
            Height = 28
        };
        if (!double.IsNaN(width)) box.Width = width;
        return box;
    }

    public static ComboBox Combo(double width = double.NaN)
    {
        var box = new ComboBox
        {
            Margin = new Thickness(0, 3, 8, 3),
            Width = double.IsNaN(width) ? 160 : width,
            FontFamily = Font,
            FontSize = BodySize,
            Foreground = TextDark,
            Background = Brushes.White,
            BorderBrush = Line,
            Height = 28,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        return box;
    }

    /// <summary>列表：白底、Gray6 描边、圆角 5，选中行 ColorBrush7。</summary>
    public static ListBox List(double height = double.NaN)
    {
        var list = new ListBox
        {
            Margin = new Thickness(0, 4, 0, 4),
            Background = Brushes.White,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3),
            FontFamily = Font,
            FontSize = BodySize,
            Foreground = TextDark,
            ItemContainerStyle = PclListItemStyle()
        };
        if (!double.IsNaN(height)) list.Height = height;
        return list;
    }

    private static Style PclListItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 1, 0, 1)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Font));
        style.Setters.Add(new Setter(Control.FontSizeProperty, BodySize));
        style.Setters.Add(new Setter(Control.ForegroundProperty, TextDark));

        var template = new ControlTemplate(typeof(ListBoxItem));
        var root = new FrameworkElementFactory(typeof(Border), "PART_Item");
        root.SetValue(Border.CornerRadiusProperty, new CornerRadius(ItemRadius));
        root.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        root.SetValue(Border.PaddingProperty, new Thickness(8, 6, 8, 6));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(presenter);
        template.VisualTree = root;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, HoverBg, "PART_Item"));
        template.Triggers.Add(hover);

        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, SelectionBg, "PART_Item"));
        template.Triggers.Add(selected);

        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand));
        return style;
    }

    /// <summary>子页切换器：左侧列表当作 PCL 的页内导航。</summary>
    public static Grid SubPager(IList<UIElement> panes, ListBox nav, params string[] titles)
    {
        var host = new Grid();
        for (var i = 0; i < panes.Count; i++)
        {
            panes[i].Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            host.Children.Add(panes[i]);
        }
        nav.Items.Clear();
        foreach (var title in titles) nav.Items.Add(title);
        nav.SelectedIndex = 0;
        nav.SelectionChanged += (_, _) =>
        {
            var index = nav.SelectedIndex;
            for (var i = 0; i < panes.Count; i++)
                panes[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        };
        return host;
    }

    /// <summary>细线分隔（PCL 用 Gray6）。</summary>
    public static Border SeparatorLine(double topBottom = 10)
    {
        return new Border
        {
            Height = 1,
            Background = Line,
            Margin = new Thickness(0, topBottom, 0, topBottom)
        };
    }

    /// <summary>PCL 风格的“已就绪 / 进行中”小圆点。</summary>
    public static Ellipse Dot(Brush color, double size = 8) => new()
    {
        Width = size,
        Height = size,
        Fill = color,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 6, 0)
    };
}
