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
    private const double HoverRadius = 46;
    private const int ColorSteps = 8;
    private const int HeadColor = ColorSteps;
    private const int AlphaLevels = 28;
    private const double MinTrailAlpha = 0.10;
    private const double MaxTrailAlpha = 0.58;
    private const double MinFrameSeconds = 0.011;

    private static readonly Color Base = Color.FromRgb(0x4F, 0x72, 0xC4);
    private static readonly Color Hover = Color.FromRgb(0x5C, 0xB2, 0xFF);
    private static readonly Color Head = Color.FromRgb(0xC8, 0xE0, 0xFF);

    private readonly Random _random = new();
    private readonly Dictionary<int, FormattedText> _cache = new();
    private readonly Typeface _typeface = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private readonly VisualCollection _visuals;
    private readonly DrawingVisual _overlay = new();

    private Column[] _columns = Array.Empty<Column>();
    private float[][] _heat = Array.Empty<float[]>();
    private char[][] _grid = Array.Empty<char[]>();
    private int _rows;
    private TimeSpan _lastTime;
    private bool _animating;
    private bool _heatActive;
    private bool _overlayDrawn;

    private sealed class Column
    {
        public double X;
        public double Head;
        public double Speed;
        public int Length;
        public char[] Trail = Array.Empty<char>();
        public double NextMutation;
        public DrawingVisual Visual = new();
        public TranslateTransform Move = new();
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
        _visuals = new VisualCollection(this);
        IsHitTestVisible = false;
        ClipToBounds = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => Rebuild();
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

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
        _visuals.Clear();

        var cols = (int)(ActualWidth / CellWidth) + 1;
        _rows = (int)(ActualHeight / CellHeight) + 2;
        if (cols <= 0 || _rows <= 0)
        {
            _columns = Array.Empty<Column>();
            return;
        }

        _columns = new Column[cols];
        _heat = new float[cols][];
        _grid = new char[cols][];

        for (var c = 0; c < cols; c++)
        {
            _heat[c] = new float[_rows + 1];
            _grid[c] = new char[_rows + 1];
            for (var r = 0; r < _grid[c].Length; r++)
                _grid[c][r] = RandomGlyph();

            var column = new Column { X = c * CellWidth };
            column.Visual.Transform = column.Move;
            Respawn(column, spread: true);
            _columns[c] = column;
            _visuals.Add(column.Visual);
        }

        _visuals.Add(_overlay);
    }

    private void Respawn(Column column, bool spread)
    {
        column.Speed = 4 + _random.NextDouble() * 7;
        column.Length = 8 + _random.Next(15);
        column.Head = spread
            ? _random.NextDouble() * (_rows + column.Length) - column.Length
            : -_random.NextDouble() * _rows * 0.6;
        column.Trail = new char[column.Length];
        for (var k = 0; k < column.Length; k++)
            column.Trail[k] = RandomGlyph();
        column.NextMutation = 0.25 + _random.NextDouble() * 0.7;

        DrawStrip(column);
        Place(column);
    }

    private char RandomGlyph() => Alphabet[_random.Next(Alphabet.Length)];

    private void Place(Column column)
    {
        column.Move.X = column.X;
        column.Move.Y = (column.Head - (column.Length - 1)) * CellHeight;
    }

    private void DrawStrip(Column column)
    {
        using var dc = column.Visual.RenderOpen();
        for (var k = 0; k < column.Length; k++)
        {
            double alpha;
            int colorStep;
            if (k == 0)
            {
                alpha = 0.98;
                colorStep = HeadColor;
            }
            else
            {
                var fade = 1.0 - (double)k / column.Length;
                alpha = MinTrailAlpha + (MaxTrailAlpha - MinTrailAlpha) * fade;
                colorStep = 0;
            }

            var level = LevelFor(alpha);
            if (level <= 0) continue;

            var text = GetText(column.Trail[k], colorStep, level);
            dc.DrawText(text, new Point(0, (column.Length - 1 - k) * CellHeight));
        }
    }

    private static int LevelFor(double alpha)
    {
        var level = (int)Math.Round(alpha * (AlphaLevels - 1));
        return Math.Clamp(level, 0, AlphaLevels - 1);
    }

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
    }

    private void Advance(double dt)
    {
        foreach (var column in _columns)
        {
            column.Head += column.Speed * dt;
            if (column.Head - column.Length > _rows)
            {
                Respawn(column, spread: false);
                continue;
            }

            column.NextMutation -= dt;
            if (column.NextMutation <= 0)
            {
                column.Trail[_random.Next(column.Length)] = RandomGlyph();
                column.NextMutation = 0.25 + _random.NextDouble() * 0.7;
                DrawStrip(column);
            }

            Place(column);
        }

        var mouse = CursorInElement();
        var inside = mouse.X > -HoverRadius && mouse.X < ActualWidth + HoverRadius
                     && mouse.Y > -HoverRadius && mouse.Y < ActualHeight + HoverRadius;

        if (!inside && !_heatActive)
        {
            if (_overlayDrawn)
            {
                using (_overlay.RenderOpen()) { }
                _overlayDrawn = false;
            }
            return;
        }

        UpdateHeat(mouse, dt);
        DrawOverlay(mouse);
        _overlayDrawn = true;
    }

    private void UpdateHeat(Point mouse, double dt)
    {
        var decay = (float)(dt * 0.9);
        var active = false;
        for (var c = 0; c < _heat.Length; c++)
        {
            var heat = _heat[c];
            var centerX = c * CellWidth + CellWidth / 2;
            var near = Math.Abs(centerX - mouse.X) < HoverRadius;

            for (var r = 0; r < heat.Length; r++)
            {
                var value = heat[r];
                if (value > 0) value = Math.Max(0f, value - decay);

                if (near)
                {
                    var dx = centerX - mouse.X;
                    var dy = r * CellHeight + CellHeight / 2 - mouse.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < HoverRadius)
                        value = Math.Max(value, (float)Math.Pow(1 - distance / HoverRadius, 0.7));
                }

                heat[r] = value;
                if (value > 0) active = true;
            }
        }

        _heatActive = active;
    }

    private void DrawOverlay(Point mouse)
    {
        using var dc = _overlay.RenderOpen();

        for (var c = 0; c < _columns.Length; c++)
        {
            var column = _columns[c];
            var centerX = column.X + CellWidth / 2;
            var near = Math.Abs(centerX - mouse.X) < HoverRadius;

            if (near)
            {
                for (var k = 0; k < column.Length; k++)
                {
                    var y = (column.Head - k) * CellHeight;
                    var dx = centerX - mouse.X;
                    var dy = y + CellHeight / 2 - mouse.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance >= HoverRadius) continue;

                    var t = Math.Min(1.0, Math.Pow(1 - distance / HoverRadius, 0.7) * 1.7);
                    var fade = k == 0 ? 0.98 : MinTrailAlpha + (MaxTrailAlpha - MinTrailAlpha) * (1.0 - (double)k / column.Length);
                    var level = LevelFor(Math.Max(fade, t * 0.98));
                    if (level <= 0) continue;

                    var text = GetText(column.Trail[k], (int)Math.Round(t * (ColorSteps - 1)), level);
                    dc.DrawText(text, new Point(column.X, y));
                }
            }

            var heat = _heat[c];
            for (var r = 0; r < heat.Length; r++)
            {
                if (heat[r] <= 0.01f) continue;

                var d = column.Head - r;
                if (d >= -0.5 && d <= column.Length - 0.5) continue;

                var t = Math.Min(1f, heat[r] * 1.7f);
                var level = LevelFor(t * 0.98);
                if (level <= 0) continue;

                var text = GetText(_grid[c][r], (int)Math.Round(t * (ColorSteps - 1)), level);
                dc.DrawText(text, new Point(column.X, r * CellHeight));
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
