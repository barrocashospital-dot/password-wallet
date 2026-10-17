using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Vault.App.Views;

public sealed class CodeRain : FrameworkElement
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const double CellWidth = 20;
    private const double CellHeight = 22;
    private const double GlyphSize = 16;
    private const double HoverRadius = 80;
    private const int ColorSteps = 8;
    private const int HeadColor = ColorSteps;
    private const int AlphaLevels = 12;
    private const double MinTrailAlpha = 0.10;
    private const double MaxTrailAlpha = 0.58;
    private const double MinFrameSeconds = 1.0 / 30;

    private static readonly Color Base = Color.FromRgb(0x4F, 0x72, 0xC4);
    private static readonly Color Hover = Color.FromRgb(0x5C, 0xB2, 0xFF);
    private static readonly Color Head = Color.FromRgb(0xC8, 0xE0, 0xFF);

    private readonly Random _random = new();
    private readonly Dictionary<int, FormattedText> _cache = new();
    private readonly Typeface _typeface = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private Column[] _columns = Array.Empty<Column>();
    private int _rows;
    private TimeSpan _lastTime;
    private bool _animating;

    private sealed class Column
    {
        public double Head;
        public double Speed;
        public int Length;
        public char[] Glyphs = Array.Empty<char>();
        public float[] Heat = Array.Empty<float>();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    public CodeRain()
    {
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => Rebuild();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Rebuild();
        if (!SystemParameters.ClientAreaAnimation || _animating) return;

        _lastTime = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
        _animating = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_animating) return;

        CompositionTarget.Rendering -= OnFrame;
        _animating = false;
    }

    private void Rebuild()
    {
        var cols = (int)(ActualWidth / CellWidth) + 1;
        _rows = (int)(ActualHeight / CellHeight) + 2;
        if (cols <= 0 || _rows <= 0)
        {
            _columns = Array.Empty<Column>();
            return;
        }

        _columns = new Column[cols];
        for (var c = 0; c < cols; c++)
        {
            var column = new Column
            {
                Glyphs = new char[_rows + 1],
                Heat = new float[_rows + 1]
            };
            for (var r = 0; r < column.Glyphs.Length; r++)
                column.Glyphs[r] = RandomGlyph();
            Respawn(column, spread: true);
            _columns[c] = column;
        }
        InvalidateVisual();
    }

    private void Respawn(Column column, bool spread)
    {
        column.Speed = 4 + _random.NextDouble() * 7;
        column.Length = 8 + _random.Next(15);
        column.Head = spread
            ? _random.NextDouble() * (_rows + column.Length) - column.Length
            : -_random.NextDouble() * _rows * 0.6;
    }

    private char RandomGlyph() => Alphabet[_random.Next(Alphabet.Length)];

    private Point CursorInElement()
    {
        if (!IsLoaded || PresentationSource.FromVisual(this) is null) return new Point(-10000, -10000);
        if (!GetCursorPos(out var screen)) return new Point(-10000, -10000);
        return PointFromScreen(new Point(screen.X, screen.Y));
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args) return;
        if (_lastTime == TimeSpan.Zero)
        {
            _lastTime = args.RenderingTime;
            return;
        }

        var dt = (args.RenderingTime - _lastTime).TotalSeconds;
        if (dt < MinFrameSeconds) return;

        _lastTime = args.RenderingTime;
        Advance(Math.Min(dt, 0.1));
        InvalidateVisual();
    }

    private void Advance(double dt)
    {
        var mouse = CursorInElement();
        var decay = (float)(dt * 0.9);

        for (var c = 0; c < _columns.Length; c++)
        {
            var column = _columns[c];
            column.Head += column.Speed * dt;
            if (column.Head - column.Length > _rows)
                Respawn(column, spread: false);

            var headRow = (int)Math.Floor(column.Head);
            if (_random.NextDouble() < dt * 6)
            {
                var row = headRow - _random.Next(column.Length);
                if (row >= 0 && row < column.Glyphs.Length)
                    column.Glyphs[row] = RandomGlyph();
            }

            var centerX = c * CellWidth + CellWidth / 2;
            var near = Math.Abs(centerX - mouse.X) < HoverRadius;
            for (var r = 0; r < column.Heat.Length; r++)
            {
                var heat = Math.Max(0f, column.Heat[r] - decay);

                if (near)
                {
                    var dx = centerX - mouse.X;
                    var dy = r * CellHeight + CellHeight / 2 - mouse.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < HoverRadius)
                        heat = Math.Max(heat, (float)Math.Pow(1 - distance / HoverRadius, 0.7));
                }

                column.Heat[r] = heat;
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        for (var c = 0; c < _columns.Length; c++)
        {
            var column = _columns[c];
            var headRow = (int)Math.Floor(column.Head);
            var x = c * CellWidth;

            for (var row = 0; row < column.Glyphs.Length; row++)
            {
                var k = headRow - row;
                var trail = 0.0;
                if (k >= 0 && k < column.Length)
                {
                    var fade = 1.0 - (double)k / column.Length;
                    trail = MinTrailAlpha + (MaxTrailAlpha - MinTrailAlpha) * fade;
                    if (k == 0) trail = 0.98;
                }

                var heat = Math.Min(1f, column.Heat[row] * 1.7f);
                var alpha = Math.Max(trail, heat * 0.98);
                var level = (int)Math.Round(alpha * (AlphaLevels - 1));
                if (level <= 0) continue;
                level = Math.Min(level, AlphaLevels - 1);

                var colorStep = k == 0 && heat < 0.5f
                    ? HeadColor
                    : (int)Math.Round(heat * (ColorSteps - 1));

                var text = GetText(column.Glyphs[row], colorStep, level);
                dc.DrawText(text, new Point(x, row * CellHeight));
            }
        }
    }

    private FormattedText GetText(char glyph, int colorStep, int alphaLevel)
    {
        var key = (glyph * (ColorSteps + 1) + colorStep) * AlphaLevels + alphaLevel;
        if (_cache.TryGetValue(key, out var cached)) return cached;

        Color rgb;
        if (colorStep == HeadColor)
        {
            rgb = Head;
        }
        else
        {
            var mix = colorStep / (double)(ColorSteps - 1);
            rgb = Color.FromRgb(
                (byte)Math.Round(Base.R + (Hover.R - Base.R) * mix),
                (byte)Math.Round(Base.G + (Hover.G - Base.G) * mix),
                (byte)Math.Round(Base.B + (Hover.B - Base.B) * mix));
        }

        var color = Color.FromArgb((byte)Math.Round(255.0 * alphaLevel / (AlphaLevels - 1)), rgb.R, rgb.G, rgb.B);
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var text = new FormattedText(
            glyph.ToString(),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface,
            GlyphSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        _cache[key] = text;
        return text;
    }
}
