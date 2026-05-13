using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AthenaCompanion;

internal enum BehaviorMode
{
    Walk,
    Pose
}

internal enum AthenaInteractionMode
{
    None,
    Voice,
    Text,
    Music
}

internal static class WalkingThoughtText
{
    private static readonly string[] VariantValues = ["Hmm ...", "Ah ...", "...", ". . . ."];

    public static IReadOnlyList<string> Variants => VariantValues;

    public static int SelectIndex(double elapsedSeconds)
    {
        var safeSeconds = Math.Max(0, elapsedSeconds);
        return ((int)(safeSeconds / RotationSeconds)) % VariantValues.Length;
    }

    private const double RotationSeconds = 4.0;
}

internal sealed class AmbientSoundPlayer : IDisposable
{
    private const string AmbientSoundFileName = "on-a-day-like-today.mp3";
    private readonly Music.MusicPlaybackEngine? _player;
    private bool _shouldPlay;

    private AmbientSoundPlayer(Music.MusicPlaybackEngine? player)
    {
        _player = player;
    }

    public static AmbientSoundPlayer Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "Assets", "Sounds", AmbientSoundFileName);
        if (!File.Exists(path))
        {
            return new AmbientSoundPlayer(null);
        }

        try
        {
            var player = new Music.MusicPlaybackEngine();
            var sound = new AmbientSoundPlayer(player);
            player.PlaybackStopped += sound.OnPlaybackStopped;
            sound._ambientPath = path;
            return sound;
        }
        catch
        {
            return new AmbientSoundPlayer(null);
        }
    }

    public void Play()
    {
        if (_player is null)
        {
            return;
        }

        _shouldPlay = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(_ambientPath))
            {
                _player.Play(_ambientPath);
            }
        }
        catch
        {
            _shouldPlay = false;
        }
    }

    public void Pause()
    {
        _shouldPlay = false;
        try
        {
            _player?.Pause();
        }
        catch
        {
            // Ambient audio should never interrupt Athena's main interaction paths.
        }
    }

    public void Dispose()
    {
        if (_player is null)
        {
            return;
        }

        _shouldPlay = false;
        _player.PlaybackStopped -= OnPlaybackStopped;
        _player.Close();
    }

    private string? _ambientPath;

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        if (_player is null || !_shouldPlay)
        {
            return;
        }

        Play();
    }
}

internal sealed record AnimationClip(string Name, int StartFrame, int FrameCount, double FramesPerSecond, bool PingPong);

internal sealed class SpriteAtlas
{
    private readonly IReadOnlyList<IImage> _frames;

    private SpriteAtlas(IReadOnlyList<IImage> frames, SpriteAtlasManifest manifest)
    {
        _frames = frames;
        WalkClip = manifest.CreateWalkClip(_frames.Count);
        PoseClip = manifest.CreatePoseClip(_frames.Count);
        BarkClip = manifest.CreateBarkClip(_frames.Count);
    }

    public AnimationClip WalkClip { get; }

    public AnimationClip PoseClip { get; }

    public AnimationClip? BarkClip { get; }

    public static SpriteAtlas Load() => Load("athena-atlas.json", "athena-atlas.png");

    public static SpriteAtlas Load(string manifestFileName, string defaultAtlasFileName)
    {
        var spriteDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Sprites");
        var manifest = SpriteAtlasManifest.Load(spriteDirectory, manifestFileName, defaultAtlasFileName);
        var atlasPath = Path.Combine(spriteDirectory, manifest.Atlas);

        if (File.Exists(atlasPath))
        {
            var bitmap = new Bitmap(atlasPath);

            if (bitmap.PixelSize.Width >= manifest.Columns * manifest.FrameWidth &&
                bitmap.PixelSize.Height >= manifest.Rows * manifest.FrameHeight)
            {
                return new SpriteAtlas(SliceAtlas(bitmap, manifest), manifest);
            }
        }

        return new SpriteAtlas(BuildFallbackFrames(manifest), manifest);
    }

    public IImage GetFrame(AnimationClip clip, double clipSeconds)
    {
        if (_frames.Count == 0)
        {
            throw new InvalidOperationException("Sprite atlas contains no frames.");
        }

        return _frames[AnimationFrameSelector.SelectFrameIndex(clip, clipSeconds, _frames.Count)];
    }

    private static IReadOnlyList<IImage> SliceAtlas(Bitmap bitmap, SpriteAtlasManifest manifest)
    {
        var frames = new List<IImage>(manifest.Columns * manifest.Rows);

        for (var row = 0; row < manifest.Rows; row++)
        {
            for (var column = 0; column < manifest.Columns; column++)
            {
                var crop = new CroppedBitmap(bitmap, new PixelRect(
                    column * manifest.FrameWidth,
                    row * manifest.FrameHeight,
                    manifest.FrameWidth,
                    manifest.FrameHeight));
                frames.Add(crop);
            }
        }

        return frames;
    }

    private static IReadOnlyList<IImage> BuildFallbackFrames(SpriteAtlasManifest manifest)
    {
        var frames = new List<IImage>(manifest.Columns * manifest.Rows);
        for (var i = 0; i < manifest.Columns * manifest.Rows; i++)
        {
            frames.Add(RenderFallbackFrame(i));
        }

        return frames;
    }

    private static IImage RenderFallbackFrame(int frame)
    {
        var bitmap = new RenderTargetBitmap(
            new PixelSize(SpriteAtlasManifest.DefaultFrameWidth, SpriteAtlasManifest.DefaultFrameHeight),
            new Vector(96, 96));

        using (var drawing = bitmap.CreateDrawingContext())
        {
            var bob = Math.Sin(frame / 32.0 * Math.PI * 2) * 3;
            var step = Math.Sin(frame / 24.0 * Math.PI * 2) * 8;

            drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(150, 30, 18, 52)), null, new Rect(90, 215, 76, 14));
            drawing.DrawEllipse(Brushes.MediumPurple, null, new Rect(90, 30 + bob, 76, 104));
            drawing.DrawEllipse(Brushes.LavenderBlush, null, new Rect(104, 44 + bob, 48, 56));
            drawing.DrawGeometry(Brushes.WhiteSmoke, new Pen(Brushes.Gainsboro, 2), BuildRobeGeometry(128, 126 + bob, step));
            drawing.DrawLine(new Pen(Brushes.WhiteSmoke, 9), new Point(106, 144 + bob), new Point(94 - step * 0.25, 198));
            drawing.DrawLine(new Pen(Brushes.WhiteSmoke, 9), new Point(150, 144 + bob), new Point(164 + step * 0.25, 198));
            drawing.DrawLine(new Pen(Brushes.Plum, 5), new Point(111, 66 + bob), new Point(99, 75 + bob));
            drawing.DrawLine(new Pen(Brushes.Plum, 5), new Point(145, 66 + bob), new Point(157, 75 + bob));
        }

        return bitmap;
    }

    private static Geometry BuildRobeGeometry(double x, double y, double step)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(x, y - 32), isFilled: true);
            context.LineTo(new Point(x - 46, y + 82));
            context.QuadraticBezierTo(new Point(x - 18 - step * 0.2, y + 96), new Point(x, y + 86));
            context.QuadraticBezierTo(new Point(x + 18 + step * 0.2, y + 96), new Point(x + 46, y + 82));
            context.EndFigure(isClosed: true);
        }

        return geometry;
    }
}

internal sealed class SpriteAtlasManifest
{
    public const int DefaultFrameWidth = 256;
    public const int DefaultFrameHeight = 256;

    public string Atlas { get; set; } = "athena-atlas.png";
    public int Columns { get; set; } = 8;
    public int Rows { get; set; } = 4;
    public int FrameWidth { get; set; } = DefaultFrameWidth;
    public int FrameHeight { get; set; } = DefaultFrameHeight;
    public int WalkStartFrame { get; set; }
    public int WalkFrameCount { get; set; } = 24;
    public double WalkFramesPerSecond { get; set; } = 24;
    public int PoseStartFrame { get; set; } = 24;
    public int PoseFrameCount { get; set; } = 8;
    public double PoseFramesPerSecond { get; set; } = 8;
    public int BarkStartFrame { get; set; } = -1;
    public int BarkFrameCount { get; set; }
    public double BarkFramesPerSecond { get; set; } = 10;
    public bool BarkPingPong { get; set; }

    public static SpriteAtlasManifest Load(string spriteDirectory) =>
        Load(spriteDirectory, "athena-atlas.json", "athena-atlas.png");

    public static SpriteAtlasManifest Load(string spriteDirectory, string manifestFileName, string defaultAtlasFileName)
    {
        var path = Path.Combine(spriteDirectory, manifestFileName);
        if (!File.Exists(path))
        {
            return new SpriteAtlasManifest { Atlas = defaultAtlasFileName };
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var manifest = JsonSerializer.Deserialize<SpriteAtlasManifest>(File.ReadAllText(path), options) ??
            new SpriteAtlasManifest { Atlas = defaultAtlasFileName };
        if (string.IsNullOrWhiteSpace(manifest.Atlas))
        {
            manifest.Atlas = defaultAtlasFileName;
        }

        manifest.Normalize();
        return manifest;
    }

    public AnimationClip CreateWalkClip(int frameTotal) =>
        CreateClip("Walk", WalkStartFrame, WalkFrameCount, WalkFramesPerSecond, pingPong: false, frameTotal);

    public AnimationClip CreatePoseClip(int frameTotal) =>
        CreateClip("Pose", PoseStartFrame, PoseFrameCount, PoseFramesPerSecond, pingPong: true, frameTotal);

    public AnimationClip? CreateBarkClip(int frameTotal) =>
        BarkStartFrame < 0 || BarkFrameCount <= 0
            ? null
            : CreateClip("Bark", BarkStartFrame, BarkFrameCount, BarkFramesPerSecond, BarkPingPong, frameTotal);

    private static AnimationClip CreateClip(
        string name,
        int startFrame,
        int frameCount,
        double framesPerSecond,
        bool pingPong,
        int frameTotal)
    {
        startFrame = Math.Clamp(startFrame, 0, Math.Max(0, frameTotal - 1));
        frameCount = Math.Clamp(frameCount, 1, Math.Max(1, frameTotal - startFrame));
        framesPerSecond = Math.Max(1, framesPerSecond);
        return new AnimationClip(name, startFrame, frameCount, framesPerSecond, pingPong);
    }

    private void Normalize()
    {
        Columns = Math.Max(1, Columns);
        Rows = Math.Max(1, Rows);
        FrameWidth = Math.Max(1, FrameWidth);
        FrameHeight = Math.Max(1, FrameHeight);
        WalkFrameCount = Math.Max(1, WalkFrameCount);
        PoseFrameCount = Math.Max(1, PoseFrameCount);
        BarkFrameCount = Math.Max(0, BarkFrameCount);
    }
}
