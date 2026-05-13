using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AthenaCompanion.Music;
using AthenaCompanion.Security;
using AthenaCompanion.Settings;
using AthenaCompanion.TextChat;
using AthenaCompanion.Tools;
using AthenaCompanion.UI;
using AthenaCompanion.UI.Interop;
using AthenaCompanion.Voice;

namespace AthenaCompanion;

public sealed class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();
    private readonly AthenaSettings _settings = AthenaSettings.Load();
    private readonly OpenAiKeyProvider _keyProvider = new();
    private readonly AthenaVoiceController _voiceController;
    private readonly AmbientSoundPlayer _ambientSoundPlayer;
    private readonly WalkAnimationController _walk = new(SpriteAtlas.Load());
    private readonly TrayMenuController _tray;
    private readonly Border _textModeBubble;
    private readonly TextBlock _textModeBubbleText;
    private readonly Border _voiceModeBubble;
    private readonly Button _puppyButton;
    private readonly Image _puppyIconImage;
    private readonly Border _voiceBusyIndicator;
    private readonly TextBlock _voiceBusyText;
    private readonly TextBlock _voiceBusyDots;
    private readonly Image _spriteImage;

    private double _lastSeconds;
    private int _thoughtVariantIndex = -1;
    private AthenaInteractionMode _interactionMode = AthenaInteractionMode.None;
    private bool _clickThrough;
    private string _voiceStatus = "Voice off";
    private string _busyIndicatorLabel = "Thinking";
    private TextChatWindow? _textChatWindow;
    private MusicPlayerWindow? _musicPlayerWindow;
    private OnboardingWindow? _onboardingWindow;
    private DogCompanionController? _dogController;
    private DogWindow? _dogWindow;

    private bool IsInteractionPaused => _interactionMode != AthenaInteractionMode.None;
    private double WindowLeft => Position.X;
    private double WindowTop => Position.Y;
    private double WindowWidth => Bounds.Width > 0 ? Bounds.Width : Width;
    private double WindowHeight => Bounds.Height > 0 ? Bounds.Height : Height;

    public MainWindow()
    {
        MusicDirectoryBootstrapper.Ensure(_settings);
        _voiceController = new AthenaVoiceController(() => _settings.Voice, ShowGeneratedImage, OpenMusicPlayerFromTool);
        _tray = new TrayMenuController(new TrayMenuStateProvider(
            () => _interactionMode,
            () => _clickThrough,
            () => _voiceStatus,
            () => _settings.Voice,
            () => _voiceController.GetKeyStatus()));
        _tray.PauseRequested += (_, _) => TogglePause();
        _tray.ClickThroughToggled += (_, _) => ToggleClickThrough();
        _tray.TextModeRequested += (_, _) => ToggleTextMode();
        _tray.MusicRequested += (_, _) => ToggleMusicMode();
        _tray.VoiceChanged += (_, voice) => ChangeVoice(voice);
        _tray.ConfigureApiKeyRequested += OnConfigureApiKeyRequested;
        _tray.RemoveApiKeyRequested += OnRemoveApiKeyRequested;
        _tray.OnboardingRequested += OnOnboardingRequested;
        _tray.ExitRequested += (_, _) => Close();

        (_textModeBubble, _textModeBubbleText) = BuildTextBubble();
        _textModeBubble.PointerReleased += OnTextModeBubblePointerReleased;
        _voiceModeBubble = BuildVoiceBubble();
        (_puppyButton, _puppyIconImage) = BuildPuppyButton();
        (_voiceBusyIndicator, _voiceBusyText, _voiceBusyDots) = BuildBusyIndicator();
        _spriteImage = new Image
        {
            Width = 160,
            Height = 144,
            ZIndex = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Stretch = Stretch.Uniform
        };

        Title = "Athena Companion";
        Width = 190;
        Height = 176;
        Background = Brushes.Transparent;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Icon = IconLoader.LoadWindowIcon();
        Content = BuildContent();

        _ambientSoundPlayer = AmbientSoundPlayer.Load(AppContext.BaseDirectory);
        _timer.Tick += OnTick;
        _voiceController.StatusChanged += OnVoiceStatusChanged;
        _voiceController.Error += OnVoiceError;
        UpdateInteractionVisuals();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _tray.Initialize();
        RefreshTrackBounds(resetPosition: true);
        _clock.Start();
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        UpdateAmbientSoundState();
        _timer.Start();
        _ = ShowFirstRunOnboardingIfNeededAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _voiceController.StatusChanged -= OnVoiceStatusChanged;
        _voiceController.Error -= OnVoiceError;
        _ = _voiceController.DisposeAsync();
        CloseTextChatWindow();
        CloseMusicPlayerWindow();
        CloseDogWindow();
        _ambientSoundPlayer.Dispose();
        _tray.Dispose();
        base.OnClosed(e);
    }

    private Grid BuildContent()
    {
        var root = new Grid
        {
            Background = Brushes.Transparent
        };
        root.PointerReleased += OnWindowPointerReleased;
        root.Children.Add(_textModeBubble);
        root.Children.Add(_voiceModeBubble);
        root.Children.Add(_puppyButton);
        root.Children.Add(_voiceBusyIndicator);
        root.Children.Add(_spriteImage);
        return root;
    }

    private static (Border Bubble, TextBlock Text) BuildTextBubble()
    {
        var text = new TextBlock
        {
            Text = "Hmm ...",
            Foreground = Brush("#F7F1FF"),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold
        };
        var bubble = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ZIndex = 2,
            Margin = new Thickness(0, 10, 55, 0),
            Padding = new Thickness(10, 5),
            Background = Brush("#E8262233"),
            BorderBrush = Brush("#B8D9CCFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = text
        };
        ToolTip.SetTip(bubble, "Open text chat");
        return (bubble, text);
    }

    private Border BuildVoiceBubble()
    {
        var text = new TextBlock
        {
            Text = "Mic",
            Foreground = Brush("#F7F1FF"),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var bubble = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ZIndex = 2,
            Margin = new Thickness(18, 24, 0, 0),
            Padding = new Thickness(8, 5),
            Background = Brush("#E8262233"),
            BorderBrush = Brush("#B8D9CCFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = text
        };
        bubble.PointerReleased += OnVoiceModeBubblePointerReleased;
        ToolTip.SetTip(bubble, "Enter voice chat");
        return bubble;
    }

    private (Button Button, Image Image) BuildPuppyButton()
    {
        var image = new Image
        {
            Source = LoadBitmap(Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "puppy-icon.png")),
            Stretch = Stretch.Uniform
        };
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ZIndex = 2,
            Margin = new Thickness(0, 23, 18, 0),
            Width = 31,
            Height = 31,
            Padding = new Thickness(4),
            Background = Brush("#D8241F2F"),
            BorderBrush = Brush("#B8FFF0B8"),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = image
        };
        button.Click += OnPuppyButtonClick;
        ToolTip.SetTip(button, "Spawn puppy");
        return (button, image);
    }

    private static (Border Border, TextBlock Text, TextBlock Dots) BuildBusyIndicator()
    {
        var text = new TextBlock
        {
            Foreground = Brush("#F7F1FF"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Text = "Thinking"
        };
        var dots = new TextBlock
        {
            Width = 16,
            Foreground = Brush("#F7F1FF"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Text = "..."
        };
        var border = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            ZIndex = 3,
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(8, 3),
            Background = Brush("#E8201D2B"),
            BorderBrush = Brush("#A8D9CCFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
            IsVisible = false,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { text, dots }
            }
        };
        return (border, text, dots);
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            _tray.ShowContextMenu((Control)sender!);
            e.Handled = true;
            return;
        }

        if (e.InitialPressMouseButton != MouseButton.Left || _clickThrough)
        {
            return;
        }

        if (ReferenceEquals(e.Source, _textModeBubble) || IsWithin(_textModeBubble, e.Source as Control) ||
            ReferenceEquals(e.Source, _voiceModeBubble) || IsWithin(_voiceModeBubble, e.Source as Control) ||
            ReferenceEquals(e.Source, _puppyButton) || IsWithin(_puppyButton, e.Source as Control))
        {
            e.Handled = true;
            return;
        }

        TogglePause();
        e.Handled = true;
    }

    private void OnVoiceModeBubblePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_clickThrough || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        TogglePause();
        e.Handled = true;
    }

    private void OnTextModeBubblePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_clickThrough || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        ToggleTextMode();
        e.Handled = true;
    }

    private void OnPuppyButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_clickThrough)
        {
            return;
        }

        TogglePuppy();
        e.Handled = true;
    }

    private static bool IsWithin(Control parent, Control? source)
    {
        for (var current = source; current is not null; current = current.Parent as Control)
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }
        }

        return false;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.08);
        _lastSeconds = now;

        _walk.Tick(now, dt, IsInteractionPaused);

        SetWindowPosition(_walk.X, WindowTop);
        _spriteImage.Source = _walk.CurrentFrame(now);
        _spriteImage.RenderTransform = _walk.Direction < 0
            ? new ScaleTransform(-1, 1)
            : null;
        UpdateDog(now, dt);
        UpdateWalkingThoughtText(now);
        UpdateBusyIndicatorAnimation(now);
    }

    private void RefreshTrackBounds(bool resetPosition)
    {
        var bounds = MonitorGeometry.GetTrackBounds(this, WindowWidth, WindowHeight, sidePadding: 8, bottomOffset: 3);
        SetWindowPosition(WindowLeft, bounds.Top);
        _walk.SetTrackBounds(bounds.MinX, bounds.MaxX, resetPosition);
        SetWindowPosition(_walk.X, bounds.Top);
    }

    private void SetWindowPosition(double left, double top) =>
        Position = new PixelPoint((int)Math.Round(left), (int)Math.Round(top));

    private void TogglePause()
    {
        if (_interactionMode == AthenaInteractionMode.Voice)
        {
            ResumeWalking();
        }
        else
        {
            EnterVoiceMode();
        }

        _tray.Refresh();
    }

    private void ToggleTextMode()
    {
        if (_interactionMode == AthenaInteractionMode.Text)
        {
            _textChatWindow?.Activate();
        }
        else
        {
            EnterTextMode();
        }

        _tray.Refresh();
    }

    private void ToggleMusicMode()
    {
        if (_interactionMode == AthenaInteractionMode.Music)
        {
            _musicPlayerWindow?.Activate();
        }
        else
        {
            EnterMusicMode(MusicPlayerRequest.OpenLibrary);
        }

        _tray.Refresh();
    }

    private void EnterVoiceMode()
    {
        CloseTextChatWindow();
        CloseMusicPlayerWindow();
        _interactionMode = AthenaInteractionMode.Voice;
        _walk.EnterPose(_clock.Elapsed.TotalSeconds, brief: false);
        UpdateInteractionVisuals();
        UpdateAmbientSoundState();
        StartVoiceMode();
    }

    private async void EnterTextMode()
    {
        StopVoiceMode();
        CloseMusicPlayerWindow();
        _interactionMode = AthenaInteractionMode.Text;
        _walk.EnterPose(_clock.Elapsed.TotalSeconds, brief: false);
        UpdateBusyIndicatorState("Text ready");
        UpdateInteractionVisuals();
        UpdateAmbientSoundState();
        await OpenTextChatWindowAsync();
    }

    private void EnterMusicMode(MusicPlayerRequest request)
    {
        StopVoiceMode();
        CloseTextChatWindow();
        _interactionMode = AthenaInteractionMode.Music;
        _walk.EnterPose(_clock.Elapsed.TotalSeconds, brief: false);
        UpdateBusyIndicatorState("Music mode");
        UpdateInteractionVisuals();
        UpdateAmbientSoundState();
        OpenMusicPlayerWindow(request);
        _tray.Refresh();
    }

    private void ResumeWalking()
    {
        StopVoiceMode();
        CloseTextChatWindow();
        CloseMusicPlayerWindow();
        _interactionMode = AthenaInteractionMode.None;
        _walk.EnterWalk(_clock.Elapsed.TotalSeconds);
        UpdateBusyIndicatorState("Ready");
        UpdateInteractionVisuals();
        UpdateAmbientSoundState();
        _tray.Refresh();
    }

    private void ToggleClickThrough()
    {
        _clickThrough = !_clickThrough;
        ClickThroughInterop.Apply(this, _clickThrough);
        UpdateInteractionVisuals();
        _tray.Refresh();
    }

    private async void ChangeVoice(string voice)
    {
        if (!RealtimeVoiceOptions.IsSupported(voice) ||
            string.Equals(_settings.Voice, voice, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.Voice = voice;
        _settings.Save();

        if (_interactionMode == AthenaInteractionMode.Voice)
        {
            await _voiceController.StopAsync();
            await _voiceController.StartAsync(this);
        }

        _tray.Refresh();
    }

    private async void OnConfigureApiKeyRequested(object? sender, EventArgs e)
    {
        await _voiceController.ConfigureApiKeyAsync(this);
        _tray.Refresh();
    }

    private void OnRemoveApiKeyRequested(object? sender, EventArgs e)
    {
        _voiceController.RemoveSavedApiKey();
        _tray.Refresh();
    }

    private void TogglePuppy()
    {
        if (_dogWindow is null)
        {
            OpenDogWindow();
        }
        else
        {
            CloseDogWindow();
        }

        UpdatePuppyButtonVisual();
    }

    private void OpenDogWindow()
    {
        if (_dogWindow is not null)
        {
            _dogWindow.Activate();
            return;
        }

        _dogController = new DogCompanionController();
        var dogWindow = new DogWindow();
        dogWindow.Closed += OnDogWindowClosed;
        _dogWindow = dogWindow;
        dogWindow.Show(this);
        UpdateDog(_clock.Elapsed.TotalSeconds, dt: 0);
    }

    private void CloseDogWindow()
    {
        var dogWindow = _dogWindow;
        if (dogWindow is null)
        {
            return;
        }

        _dogWindow = null;
        _dogController = null;
        dogWindow.Closed -= OnDogWindowClosed;
        dogWindow.Close();
        UpdatePuppyButtonVisual();
    }

    private void OnDogWindowClosed(object? sender, EventArgs e)
    {
        if (sender is DogWindow dogWindow)
        {
            dogWindow.Closed -= OnDogWindowClosed;
        }

        _dogWindow = null;
        _dogController = null;
        UpdatePuppyButtonVisual();
    }

    private void UpdateDog(double now, double dt)
    {
        if (_dogWindow is null || _dogController is null)
        {
            return;
        }

        var workingArea = MonitorGeometry.GetPrimaryWorkingAreaDip(this);
        var frame = new DogCompanionFrame(
            WindowLeft,
            WindowTop,
            WindowWidth,
            WindowHeight,
            workingArea.Left,
            workingArea.Right,
            _dogWindow.Width,
            _dogWindow.Height);
        var snapshot = _dogController.Tick(now, dt, frame);
        _dogWindow.Render(snapshot, now);
    }

    private void OnOnboardingRequested(object? sender, EventArgs e) => _ = ShowOnboardingAsync(markCompleted: false);

    private async void StartVoiceMode() => await _voiceController.StartAsync(this);

    private async void StopVoiceMode() => await _voiceController.StopAsync();

    private void OnVoiceStatusChanged(object? sender, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _voiceStatus = status;
            if (_interactionMode is not AthenaInteractionMode.Text and not AthenaInteractionMode.Music)
            {
                UpdateBusyIndicatorState(status);
            }

            _tray.Refresh();
        });
    }

    private void OnVoiceError(object? sender, string error)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _voiceStatus = "Voice error";
            if (_interactionMode == AthenaInteractionMode.Voice)
            {
                UpdateBusyIndicatorState(_voiceStatus);
            }

            _tray.Refresh();
            _tray.ShowBalloonTip("Athena Voice", error);
        });
    }

    private void ShowGeneratedImage(string imagePath)
    {
        var lightbox = new ImageLightboxWindow(imagePath);

        lightbox.Show(this);
        lightbox.Activate();
    }

    private async Task OpenTextChatWindowAsync()
    {
        if (_textChatWindow is not null)
        {
            _textChatWindow.Activate();
            return;
        }

        var apiKey = await GetOrPromptOpenAiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ResumeWalking();
            return;
        }

        var tools = new AthenaToolExecutor(
            () => apiKey,
            ShowGeneratedImage,
            status => Dispatcher.UIThread.Post(() => UpdateBusyIndicatorState(status)),
            OpenMusicPlayerFromTool);
        var session = new AthenaTextChatSession(apiKey, tools);
        var chatWindow = new TextChatWindow(session);

        chatWindow.StatusChanged += OnTextChatStatusChanged;
        chatWindow.Closed += OnTextChatClosed;
        PositionChildWindow(chatWindow);
        _textChatWindow = chatWindow;
        chatWindow.Show(this);
        chatWindow.Activate();
    }

    private void CloseTextChatWindow()
    {
        var chatWindow = _textChatWindow;
        if (chatWindow is null)
        {
            return;
        }

        _textChatWindow = null;
        chatWindow.StatusChanged -= OnTextChatStatusChanged;
        chatWindow.Closed -= OnTextChatClosed;
        chatWindow.Close();
    }

    private void OpenMusicPlayerFromTool(MusicPlayerRequest request) => EnterMusicMode(request);

    private void OpenMusicPlayerWindow(MusicPlayerRequest request)
    {
        if (_musicPlayerWindow is not null)
        {
            _musicPlayerWindow.ApplyRequest(request);
            _musicPlayerWindow.Activate();
            return;
        }

        var musicWindow = new MusicPlayerWindow(_settings.MusicDirectory);

        musicWindow.Closed += OnMusicPlayerClosed;
        PositionChildWindow(musicWindow);
        _musicPlayerWindow = musicWindow;
        musicWindow.Show(this);
        musicWindow.ApplyRequest(request);
        musicWindow.Activate();
    }

    private void CloseMusicPlayerWindow()
    {
        var musicWindow = _musicPlayerWindow;
        if (musicWindow is null)
        {
            return;
        }

        _musicPlayerWindow = null;
        musicWindow.Closed -= OnMusicPlayerClosed;
        musicWindow.Close();
    }

    private async Task<string?> GetOrPromptOpenAiKeyAsync()
    {
        var lookup = _keyProvider.TryGetApiKey();
        if (!string.IsNullOrWhiteSpace(lookup.ApiKey))
        {
            return lookup.ApiKey;
        }

        var dialog = new ApiKeySetupWindow();
        if (await dialog.ShowDialog<bool?>(this) != true)
        {
            return null;
        }

        _keyProvider.SaveApiKey(dialog.ApiKey);
        _tray.Refresh();
        return dialog.ApiKey;
    }

    private async Task ShowFirstRunOnboardingIfNeededAsync()
    {
        if (_settings.HasCompletedOnboarding)
        {
            return;
        }

        await ShowOnboardingAsync(markCompleted: true);
    }

    private async Task ShowOnboardingAsync(bool markCompleted)
    {
        if (_onboardingWindow is not null)
        {
            _onboardingWindow.Activate();
            return;
        }

        var onboardingWindow = new OnboardingWindow(_settings.MusicDirectory, async owner =>
        {
            await _voiceController.ConfigureApiKeyAsync(owner);
            _tray.Refresh();
        });

        _onboardingWindow = onboardingWindow;
        try
        {
            await onboardingWindow.ShowDialog<bool?>(this);
        }
        finally
        {
            _onboardingWindow = null;
            if (markCompleted)
            {
                _settings.HasCompletedOnboarding = true;
                _settings.Save();
            }

            _tray.Refresh();
        }
    }

    private void PositionChildWindow(Window childWindow)
    {
        var workingArea = MonitorGeometry.GetPrimaryWorkingAreaDip(this);
        var left = WindowLeft + WindowWidth + 8;
        if (left + childWindow.Width > workingArea.Right)
        {
            left = WindowLeft - childWindow.Width - 8;
        }

        var top = Math.Clamp(WindowTop - childWindow.Height + WindowHeight, workingArea.Top + 8, workingArea.Bottom - childWindow.Height - 8);
        left = Math.Clamp(left, workingArea.Left + 8, workingArea.Right - childWindow.Width - 8);
        childWindow.Position = new PixelPoint((int)Math.Round(left), (int)Math.Round(top));
    }

    private void OnTextChatStatusChanged(object? sender, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateBusyIndicatorState(status);
            _tray.Refresh();
        });
    }

    private void OnTextChatClosed(object? sender, EventArgs e)
    {
        if (sender is TextChatWindow chatWindow)
        {
            chatWindow.StatusChanged -= OnTextChatStatusChanged;
            chatWindow.Closed -= OnTextChatClosed;
        }

        _textChatWindow = null;
        if (_interactionMode == AthenaInteractionMode.Text)
        {
            ResumeWalking();
        }
    }

    private void OnMusicPlayerClosed(object? sender, EventArgs e)
    {
        if (sender is MusicPlayerWindow musicWindow)
        {
            musicWindow.Closed -= OnMusicPlayerClosed;
        }

        _musicPlayerWindow = null;
        if (_interactionMode == AthenaInteractionMode.Music)
        {
            ResumeWalking();
        }
    }

    private void UpdateBusyIndicatorState(string status)
    {
        _busyIndicatorLabel = status switch
        {
            "Connecting..." => "Connecting",
            "Thinking" => "Thinking",
            "Using tool" => "Thinking",
            "Looking at screen" => "Looking",
            "Creating image" => "Drawing",
            "Text ready" => _interactionMode == AthenaInteractionMode.Text ? "Chat" : string.Empty,
            "Music mode" => _interactionMode == AthenaInteractionMode.Music ? "Music" : string.Empty,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(_busyIndicatorLabel))
        {
            _voiceBusyIndicator.IsVisible = false;
            return;
        }

        _voiceBusyText.Text = _busyIndicatorLabel;
        _voiceBusyIndicator.IsVisible = true;
    }

    private void UpdateInteractionVisuals()
    {
        var showWalkingBubbles = !_clickThrough && _interactionMode == AthenaInteractionMode.None;
        _textModeBubble.IsVisible = showWalkingBubbles;
        _voiceModeBubble.IsVisible = showWalkingBubbles;
        _puppyButton.IsVisible = showWalkingBubbles;

        if (showWalkingBubbles)
        {
            UpdateWalkingThoughtText(_clock.Elapsed.TotalSeconds);
        }

        UpdatePuppyButtonVisual();
    }

    private void UpdatePuppyButtonVisual()
    {
        var isSpawned = _dogWindow is not null;
        _puppyButton.Opacity = isSpawned ? 1.0 : 0.82;
        ToolTip.SetTip(_puppyButton, isSpawned ? "Dismiss puppy" : "Spawn puppy");
    }

    private void UpdateWalkingThoughtText(double now)
    {
        if (_interactionMode != AthenaInteractionMode.None)
        {
            return;
        }

        var variantIndex = WalkingThoughtText.SelectIndex(now);
        if (variantIndex == _thoughtVariantIndex)
        {
            return;
        }

        _thoughtVariantIndex = variantIndex;
        _textModeBubbleText.Text = WalkingThoughtText.Variants[variantIndex];
    }

    private void UpdateAmbientSoundState()
    {
        if (_interactionMode == AthenaInteractionMode.None)
        {
            _ambientSoundPlayer.Play();
        }
        else
        {
            _ambientSoundPlayer.Pause();
        }
    }

    private void UpdateBusyIndicatorAnimation(double now)
    {
        if (!_voiceBusyIndicator.IsVisible)
        {
            return;
        }

        var dotCount = (int)(now * 2.6) % 4;
        _voiceBusyDots.Text = new string('.', dotCount).PadRight(3);
        _voiceBusyIndicator.Opacity = 0.82 + Math.Sin(now * 5.0) * 0.12;
    }

    private static IImage? LoadBitmap(string path) => File.Exists(path) ? new Bitmap(path) : null;

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
