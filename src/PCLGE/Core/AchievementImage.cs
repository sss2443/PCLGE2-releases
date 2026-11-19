using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pclge.Core;

/// <summary>生成原版风格的成就达成图片（320×64 PNG）。</summary>
public static class AchievementImage
{
    public static string Render(string title, string description1, string description2, string iconText, string destPath)
    {
        const int width = 320;
        const int height = 64;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // 背景
            var bg = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21));
            var border = new Pen(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), 2);
            dc.DrawRectangle(bg, border, new Rect(0, 0, width, height));

            // 图标框
            var iconRect = new Rect(8, 8, 48, 48);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)),
                new Pen(new SolidColorBrush(Color.FromRgb(0x7A, 0x7A, 0x7A)), 1), iconRect);

            var titleBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x55));
            var descBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            dc.DrawText(Text(title, 18, titleBrush, true), new Point(66, 6));
            dc.DrawText(Text(string.IsNullOrWhiteSpace(description1) ? " " : description1, 14, descBrush, false), new Point(66, 28));
            dc.DrawText(Text(string.IsNullOrWhiteSpace(description2) ? " " : description2, 14, descBrush, false), new Point(66, 44));

            if (!string.IsNullOrWhiteSpace(iconText))
            {
                var icon = Text(iconText.Length > 2 ? iconText[..2] : iconText, 22, new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)), true);
                icon.TextAlignment = TextAlignment.Center;
                icon.MaxTextWidth = 48;
                dc.DrawText(icon, new Point(8, 18));
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        var folder = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        using var stream = File.Create(destPath);
        encoder.Save(stream);
        return destPath;
    }

    private static FormattedText Text(string content, double size, Brush brush, bool bold)
    {
        return new FormattedText(content, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal,
                bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            size, brush, 1.0);
    }
}
