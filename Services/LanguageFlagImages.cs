using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DirectoryManagement.Services;

public static class LanguageFlagImages
{
    private const int Width = 28;
    private const int Height = 18;

    private static readonly Dictionary<AppLanguage, ImageSource> Cache = new();

    public static ImageSource Get(AppLanguage language)
    {
        if (Cache.TryGetValue(language, out var cached))
        {
            return cached;
        }

        var source = Render(language);
        if (source.CanFreeze)
        {
            source.Freeze();
        }

        Cache[language] = source;
        return source;
    }

    private static ImageSource Render(AppLanguage language)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Gray, null, new Rect(0, 0, Width, Height));
            switch (language)
            {
                case AppLanguage.Tr:
                    DrawTurkey(dc);
                    break;
                case AppLanguage.Ar:
                    DrawSaudi(dc);
                    break;
                case AppLanguage.En:
                    DrawUnitedKingdom(dc);
                    break;
                case AppLanguage.Es:
                    DrawSpain(dc);
                    break;
                case AppLanguage.Ru:
                    DrawRussia(dc);
                    break;
                case AppLanguage.Fr:
                    DrawFrance(dc);
                    break;
            }

            dc.DrawRectangle(null, new Pen(BrushFromRgb(0x88, 0x88, 0x88), 0.5), new Rect(0, 0, Width, Height));
        }

        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static void DrawTurkey(DrawingContext dc)
    {
        dc.DrawRectangle(BrushFromRgb(0xE3, 0x0A, 0x17), null, new Rect(0, 0, Width, Height));
        var white = Brushes.White;
        dc.DrawEllipse(white, null, new Point(16, 9), 5.5, 5.5);
        dc.DrawEllipse(BrushFromRgb(0xE3, 0x0A, 0x17), null, new Point(17.6, 9), 4.6, 4.6);
        var star = new PathGeometry();
        var figure = new PathFigure(new Point(21.5, 9), [], false);
        const double r = 2.2;
        for (var i = 0; i < 5; i++)
        {
            var angle = -Math.PI / 2 + i * 4 * Math.PI / 5;
            figure.Segments.Add(new LineSegment(new Point(21.5 + r * Math.Cos(angle), 9 + r * Math.Sin(angle)), true));
        }

        figure.IsClosed = true;
        star.Figures.Add(figure);
        dc.DrawGeometry(white, null, star);
    }

    private static void DrawSaudi(DrawingContext dc)
    {
        dc.DrawRectangle(BrushFromRgb(0x16, 0x5B, 0x33), null, new Rect(0, 0, Width, Height));
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 7.5, Width, 3));
        dc.DrawRectangle(Brushes.White, null, new Rect(4, 12, 14, 1.2));
    }

    private static void DrawUnitedKingdom(DrawingContext dc)
    {
        dc.DrawRectangle(BrushFromRgb(0x01, 0x27, 0xA6), null, new Rect(0, 0, Width, Height));
        var white = Brushes.White;
        var red = BrushFromRgb(0xC8, 0x10, 0x2E);
        dc.DrawRectangle(white, null, new Rect(12, 0, 4, Height));
        dc.DrawRectangle(white, null, new Rect(0, 7, Width, 4));
        dc.DrawRectangle(red, null, new Rect(13, 0, 2, Height));
        dc.DrawRectangle(red, null, new Rect(0, 8, Width, 2));
        dc.DrawRectangle(white, null, new Rect(0, 0, Width, 3));
        dc.DrawRectangle(white, null, new Rect(0, 0, 3, Height));
        dc.DrawRectangle(white, null, new Rect(Width - 3, 0, 3, Height));
        dc.DrawRectangle(white, null, new Rect(0, Height - 3, Width, 3));
        dc.DrawRectangle(red, null, new Rect(0, 0, Width, 1.5));
        dc.DrawRectangle(red, null, new Rect(0, 0, 1.5, Height));
        dc.DrawRectangle(red, null, new Rect(Width - 1.5, 0, 1.5, Height));
        dc.DrawRectangle(red, null, new Rect(0, Height - 1.5, Width, 1.5));
    }

    private static void DrawSpain(DrawingContext dc)
    {
        var red = BrushFromRgb(0xAA, 0x15, 0x1B);
        var yellow = BrushFromRgb(0xF1, 0xBF, 0x00);
        dc.DrawRectangle(red, null, new Rect(0, 0, Width, 4));
        dc.DrawRectangle(yellow, null, new Rect(0, 4, Width, 10));
        dc.DrawRectangle(red, null, new Rect(0, 14, Width, 4));
    }

    private static void DrawRussia(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, 6));
        dc.DrawRectangle(BrushFromRgb(0x00, 0x39, 0xA6), null, new Rect(0, 6, Width, 6));
        dc.DrawRectangle(BrushFromRgb(0xD5, 0x2B, 0x1E), null, new Rect(0, 12, Width, 6));
    }

    private static void DrawFrance(DrawingContext dc)
    {
        var third = Width / 3.0;
        dc.DrawRectangle(BrushFromRgb(0x00, 0x23, 0x95), null, new Rect(0, 0, third, Height));
        dc.DrawRectangle(Brushes.White, null, new Rect(third, 0, third, Height));
        dc.DrawRectangle(BrushFromRgb(0xED, 0x29, 0x39), null, new Rect(third * 2, 0, third, Height));
    }

    private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}
