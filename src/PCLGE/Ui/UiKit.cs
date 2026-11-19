using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pclge.Ui;

/// <summary>页面接口：每个一级导航实现一个。</summary>
public interface IPage
{
    string Title { get; }
    Grid Build();
    void Refresh();
}

/// <summary>界面零件与配色。</summary>
public static class UiKit
{
    public static readonly Brush Primary = new SolidColorBrush(Color.FromRgb(0x3A, 0x9E, 0xE0));
    public static readonly Brush PrimaryDark = new SolidColorBrush(Color.FromRgb(0x2C, 0x80, 0xB8));
    public static readonly Brush PageBg = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF9));
    public static readonly Brush CardBg = Brushes.White;
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x80, 0x8A, 0x94));
    public static readonly Brush TextDark = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50));
    public static readonly Brush Ok = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
    public static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));
    public static readonly Brush Danger = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));

    public static readonly FontFamily Mono = new FontFamily("Consolas");

    public static Border Card(params UIElement[] children) => Card(null, children);

    public static Border Card(string title, params UIElement[] children)
    {
        var stack = new StackPanel();
        if (!string.IsNullOrEmpty(title)) stack.Children.Add(Heading(title));
        foreach (var child in children) stack.Children.Add(child);
        return new Border
        {
            Background = CardBg,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = stack
        };
    }

    public static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 15,
        FontWeight = FontWeights.Bold,
        Foreground = TextDark,
        Margin = new Thickness(0, 0, 0, 8)
    };

    public static TextBlock Label(string text, Brush brush = null) => new()
    {
        Text = text,
        Foreground = brush ?? TextDark,
        Margin = new Thickness(0, 3, 8, 3),
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap
    };

    public static TextBlock Note(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = Muted,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 6)
    };

    public static Button PrimaryButton(string text, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            Background = Primary,
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(18, 6, 18, 6),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 8, 4),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += click;
        return button;
    }

    public static Button Button(string text, RoutedEventHandler click)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 3, 8, 3),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD6, 0xDD)),
            Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFB, 0xFC)),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        b.Click += click;
        return b;
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

    public static TextBox Input(string text, double width = double.NaN)
    {
        var box = new TextBox { Text = text, Margin = new Thickness(0, 3, 8, 3), Padding = new Thickness(4, 3, 4, 3) };
        if (!double.IsNaN(width)) box.Width = width;
        return box;
    }

    public static ComboBox Combo(double width = double.NaN)
    {
        var box = new ComboBox { Margin = new Thickness(0, 3, 8, 3), Width = double.IsNaN(width) ? 160 : width };
        return box;
    }

    public static ListBox List(double height = double.NaN)
    {
        var list = new ListBox { Margin = new Thickness(0, 4, 0, 4), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE1, 0xE6, 0xEB)) };
        if (!double.IsNaN(height)) list.Height = height;
        return list;
    }

    public static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = new Thickness(0)
    };

    /// <summary>左列表 + 右内容的双栏布局。</summary>
    public static Grid TwoPane(UIElement left, UIElement right)
    {
        var grid = new Grid { Background = PageBg };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftBorder = new Border
        {
            Background = CardBg,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(10, 10, 0, 10),
            Child = Scroll(left)
        };
        var rightBorder = new Border
        {
            Background = PageBg,
            Padding = new Thickness(10, 10, 10, 10),
            Child = Scroll(right)
        };
        Grid.SetColumn(rightBorder, 1);
        grid.Children.Add(leftBorder);
        grid.Children.Add(rightBorder);
        return grid;
    }

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
}
