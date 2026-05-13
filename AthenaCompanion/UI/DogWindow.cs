using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AthenaCompanion.UI.Interop;

namespace AthenaCompanion.UI;

internal sealed class DogWindow : Window
{
    private readonly SpriteAtlas _atlas = SpriteAtlas.Load("puppy-atlas.json", "puppy-atlas.png");
    private readonly Border _barkBubble;
    private readonly TextBlock _barkBubbleText;
    private readonly Image _dogSpriteImage;

    public DogWindow()
    {
        Title = "Athena Puppy";
        Width = 130;
        Height = 116;
        Background = Brushes.Transparent;
        CanResize = false;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        _barkBubbleText = new TextBlock
        {
            Foreground = Brush("#FFFFF7DD"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Text = "woof"
        };
        _barkBubble = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            Padding = new Thickness(8, 3),
            Background = Brush("#E82B2430"),
            BorderBrush = Brush("#D8FFF0B8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            IsHitTestVisible = false,
            IsVisible = false,
            Child = _barkBubbleText
        };
        _dogSpriteImage = new Image
        {
            Width = 94,
            Height = 82,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
        };
        Content = new Grid
        {
            Background = Brushes.Transparent,
            Children = { _barkBubble, _dogSpriteImage }
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ClickThroughInterop.Apply(this, enabled: true);
    }

    public void Render(DogCompanionSnapshot snapshot, double now)
    {
        Position = new PixelPoint((int)Math.Round(snapshot.X), (int)Math.Round(snapshot.Top));

        var clip = snapshot.Mode switch
        {
            DogBehaviorMode.Bark when _atlas.BarkClip is not null => _atlas.BarkClip,
            DogBehaviorMode.Idle => _atlas.PoseClip,
            _ => _atlas.WalkClip
        };

        _dogSpriteImage.Source = _atlas.GetFrame(clip, now - snapshot.ModeStartedSeconds);
        _dogSpriteImage.RenderTransform = snapshot.Direction < 0
            ? new ScaleTransform(-1, 1)
            : null;

        if (string.IsNullOrWhiteSpace(snapshot.BarkText))
        {
            _barkBubble.IsVisible = false;
            return;
        }

        _barkBubbleText.Text = snapshot.BarkText;
        _barkBubble.IsVisible = true;
    }

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
