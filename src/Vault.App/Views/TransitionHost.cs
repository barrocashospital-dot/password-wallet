using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Vault.App.Views;

public sealed class TransitionHost : ContentControl
{
    private static readonly Duration OutDuration = new(TimeSpan.FromMilliseconds(220));
    private static readonly Duration InDuration = new(TimeSpan.FromMilliseconds(420));
    private static readonly TimeSpan InDelay = TimeSpan.FromMilliseconds(110);

    private FrameworkElement? _content;
    private Image? _snapshot;
    private RenderTargetBitmap? _pendingPhoto;

    static TransitionHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TransitionHost), new FrameworkPropertyMetadata(typeof(TransitionHost)));
        ContentProperty.OverrideMetadata(
            typeof(TransitionHost),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, null, CoerceContent));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _content = GetTemplateChild("PART_Content") as FrameworkElement;
        _snapshot = GetTemplateChild("PART_Snapshot") as Image;
    }

    private static object? CoerceContent(DependencyObject d, object? value)
    {
        if (d is TransitionHost host) host.CapturePhoto();
        return value;
    }

    private void CapturePhoto()
    {
        _pendingPhoto = null;
        if (_content is null || Content is null || !IsLoaded) return;
        if (!SystemParameters.ClientAreaAnimation) return;

        _pendingPhoto = TakePhoto(_content);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        var photo = _pendingPhoto;
        _pendingPhoto = null;
        if (photo is null || _content is null || _snapshot is null) return;

        _snapshot.Source = photo;
        _snapshot.Visibility = Visibility.Visible;
        PlayOut(_snapshot);
        PlayIn(_content);
    }

    private RenderTargetBitmap? TakePhoto(FrameworkElement element)
    {
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (width < 1 || height < 1) return null;

        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width * dpi.DpiScaleX),
            (int)Math.Ceiling(height * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return bitmap;
    }

    private void PlayOut(Image snapshot)
    {
        var move = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(move);
        snapshot.RenderTransform = group;
        snapshot.Opacity = 1;

        var fade = new DoubleAnimation(1, 0, OutDuration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) =>
        {
            snapshot.BeginAnimation(OpacityProperty, null);
            snapshot.Visibility = Visibility.Collapsed;
            snapshot.Source = null;
        };
        snapshot.BeginAnimation(OpacityProperty, fade);

        var rise = new DoubleAnimation(0, -14, OutDuration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        move.BeginAnimation(TranslateTransform.YProperty, rise);

        var shrink = new DoubleAnimation(1, 0.985, OutDuration);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        snapshot.RenderTransformOrigin = new Point(0.5, 0.5);
    }

    private void PlayIn(FrameworkElement content)
    {
        var move = new TranslateTransform(0, 26);
        var scale = new ScaleTransform(0.985, 0.985);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(move);
        content.RenderTransform = group;
        content.RenderTransformOrigin = new Point(0.5, 0.5);
        content.Opacity = 0;

        var fade = new DoubleAnimation(0, 1, InDuration)
        {
            BeginTime = InDelay,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        fade.Completed += (_, _) =>
        {
            content.BeginAnimation(OpacityProperty, null);
            content.Opacity = 1;
            content.RenderTransform = Transform.Identity;
        };
        content.BeginAnimation(OpacityProperty, fade);

        var slide = new DoubleAnimation(26, 0, InDuration)
        {
            BeginTime = InDelay,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        move.BeginAnimation(TranslateTransform.YProperty, slide);

        var grow = new DoubleAnimation(0.985, 1, InDuration)
        {
            BeginTime = InDelay,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }
}
