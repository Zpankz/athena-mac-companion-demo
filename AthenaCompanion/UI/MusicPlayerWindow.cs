using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AthenaCompanion.Music;

namespace AthenaCompanion.UI;

internal sealed class MusicPlayerWindow : Window
{
    private readonly string _musicDirectory;
    private readonly MusicPlaybackEngine _engine = new();
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock _statusText = TextBlock("#d8d0ea", 12);
    private readonly ListBox _tracksList = new() { Background = Brushes.Transparent, Foreground = Brush("#f7f1ff") };
    private readonly StackPanel _emptyState = new() { Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _emptyStateText = TextBlock("#bdb4d4", 12);
    private readonly TextBlock _positionText = TimeText("0:00", TextAlignment.Left);
    private readonly Slider _seekSlider = new() { Minimum = 0, Maximum = 1 };
    private readonly TextBlock _durationText = TimeText("0:00", TextAlignment.Right);
    private readonly Button _playPauseButton = new() { Content = "Play", Width = 58, Height = 30, Margin = new Thickness(6, 0, 0, 0) };
    private IReadOnlyList<MusicTrack> _tracks = [];
    private bool _loading;
    private bool _seeking;
    private bool _updatingProgress;

    public MusicPlayerWindow(string musicDirectory)
    {
        _musicDirectory = musicDirectory;
        Title = "Athena Radio";
        Width = 420;
        Height = 360;
        MinWidth = 360;
        MinHeight = 300;
        CanResize = true;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Content = BuildContent();
        _engine.PlaybackStopped += OnPlaybackStopped;
        _progressTimer.Tick += OnProgressTick;
        _progressTimer.Start();
        LoadLibrary();
    }

    public void ApplyRequest(MusicPlayerRequest request)
    {
        LoadLibrary();
        if (_tracks.Count == 0)
        {
            SetStatus(MusicLibraryMessages.Empty(_musicDirectory));
            return;
        }

        var snapshot = new MusicLibrarySnapshot(_musicDirectory, _tracks);
        var track = snapshot.FindBestMatch(request.Query);
        if (track is null)
        {
            SetStatus(MusicLibraryMessages.NoMatch(request.Query));
            SelectTrack(0);
            return;
        }

        var trackIndex = IndexOfTrack(track);
        SelectTrack(trackIndex);
        if (request.Autoplay)
        {
            TryPlayFromIndex(trackIndex, direction: 1);
        }
        else
        {
            SetStatus($"Ready: {track.DisplayName}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _progressTimer.Stop();
        _progressTimer.Tick -= OnProgressTick;
        _engine.PlaybackStopped -= OnPlaybackStopped;
        _engine.Dispose();
        base.OnClosed(e);
    }

    private Control BuildContent()
    {
        var root = new Grid { Background = Brush("#151419") };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var header = new DockPanel { Margin = new Thickness(14, 12, 14, 6), LastChildFill = true };
        var close = new Button { Content = "Close", Width = 72, Height = 28 };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(new StackPanel
        {
            Children =
            {
                TextBlock("#f7f1ff", 17, "Athena Radio", FontWeight.SemiBold),
                TextBlock("#bdb4d4", 12, "Native playback")
            }
        });
        root.Children.Add(header);

        _statusText.Margin = new Thickness(14, 0, 14, 8);
        _statusText.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(_statusText, 1);
        root.Children.Add(_statusText);

        _tracksList.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(MusicTrack.RelativePath));
        _tracksList.SelectionChanged += OnTrackSelectionChanged;
        _tracksList.DoubleTapped += OnTrackDoubleTapped;
        var listBorder = new Border
        {
            Background = Brush("#201d2b"),
            BorderBrush = Brush("#3f3855"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = _tracksList
        };
        var listHost = new Grid { Margin = new Thickness(14, 0) };
        listHost.Children.Add(listBorder);
        _emptyState.Children.Add(TextBlock("#f7f1ff", 16, "No music found", FontWeight.SemiBold));
        _emptyStateText.Width = 320;
        _emptyStateText.Margin = new Thickness(0, 8, 0, 0);
        _emptyStateText.TextAlignment = TextAlignment.Center;
        _emptyState.Children.Add(_emptyStateText);
        listHost.Children.Add(_emptyState);
        Grid.SetRow(listHost, 2);
        root.Children.Add(listHost);

        _seekSlider.ValueChanged += OnSeekSliderValueChanged;
        _seekSlider.PointerPressed += (_, _) => _seeking = true;
        _seekSlider.PointerReleased += OnSeekCompleted;
        var seekGrid = new Grid { Margin = new Thickness(14, 10, 14, 0) };
        seekGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        seekGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        seekGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        seekGrid.Children.Add(_positionText);
        Grid.SetColumn(_seekSlider, 1);
        seekGrid.Children.Add(_seekSlider);
        Grid.SetColumn(_durationText, 2);
        seekGrid.Children.Add(_durationText);
        Grid.SetRow(seekGrid, 3);
        root.Children.Add(seekGrid);

        var footer = new DockPanel { Margin = new Thickness(14, 10, 14, 14), LastChildFill = false };
        var open = new Button { Content = "Open folder", MinWidth = 90, Height = 30 };
        open.Click += OnOpenFolder;
        footer.Children.Add(open);
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(controls, Dock.Right);
        controls.Children.Add(Button("<<", 42, OnPrevious));
        _playPauseButton.Click += OnPlayPause;
        controls.Children.Add(_playPauseButton);
        controls.Children.Add(Button(">>", 42, OnNext));
        controls.Children.Add(Button("Stop", 52, OnStop));
        footer.Children.Add(controls);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        return root;
    }

    private void LoadLibrary()
    {
        _loading = true;
        try
        {
            var selected = _tracksList.SelectedItem as MusicTrack;
            var snapshot = MusicLibrary.Load(_musicDirectory);
            _tracks = snapshot.Tracks;
            _tracksList.ItemsSource = _tracks;
            _emptyState.IsVisible = snapshot.IsEmpty;
            _tracksList.IsVisible = !snapshot.IsEmpty;
            _emptyStateText.Text = MusicLibraryMessages.Empty(snapshot.DirectoryPath);

            if (snapshot.IsEmpty)
            {
                SetStatus(MusicLibraryMessages.Empty(snapshot.DirectoryPath));
            }
            else
            {
                var selectedIndex = selected is null ? 0 : IndexOfTrack(selected);
                SelectTrack(Math.Max(0, selectedIndex));
                SetStatus("Library ready");
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private void SelectTrack(int index)
    {
        if (_tracks.Count == 0)
        {
            return;
        }

        index = Math.Clamp(index, 0, _tracks.Count - 1);
        _tracksList.SelectedItem = _tracks[index];
        _tracksList.ScrollIntoView(_tracks[index]);
    }

    private int IndexOfTrack(MusicTrack track)
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (string.Equals(_tracks[i].FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private bool TryPlayFromIndex(int startIndex, int direction)
    {
        if (_tracks.Count == 0)
        {
            SetStatus(MusicLibraryMessages.Empty(_musicDirectory));
            return false;
        }

        startIndex = Math.Clamp(startIndex, 0, _tracks.Count - 1);
        direction = direction < 0 ? -1 : 1;
        string? lastError = null;

        for (var attempt = 0; attempt < _tracks.Count; attempt++)
        {
            var index = (startIndex + attempt * direction) % _tracks.Count;
            if (index < 0)
            {
                index += _tracks.Count;
            }

            var track = _tracks[index];
            try
            {
                _engine.Play(track.FilePath);
                SelectTrack(index);
                _playPauseButton.Content = "Pause";
                SetStatus($"Tuned: {track.DisplayName}");
                UpdateProgress();
                return true;
            }
            catch (Exception ex)
            {
                lastError = $"Skipped unsupported file: {track.RelativePath} ({ex.Message})";
            }
        }

        _playPauseButton.Content = "Play";
        SetStatus(lastError ?? "No playable tracks.");
        return false;
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_tracks.Count == 0 || _tracksList.SelectedIndex < 0)
            {
                _playPauseButton.Content = "Play";
                return;
            }

            TryPlayFromIndex((_tracksList.SelectedIndex + 1) % _tracks.Count, direction: 1);
        });
    }

    private void OnProgressTick(object? sender, EventArgs e) => UpdateProgress();

    private void UpdateProgress()
    {
        var duration = _engine.Duration;
        var position = _engine.Position;
        _updatingProgress = true;
        try
        {
            _seekSlider.Maximum = Math.Max(1, duration.TotalSeconds);
            if (!_seeking)
            {
                _seekSlider.Value = Math.Clamp(position.TotalSeconds, 0, _seekSlider.Maximum);
            }

            _positionText.Text = FormatTime(position);
            _durationText.Text = duration == TimeSpan.Zero ? "--:--" : FormatTime(duration);
        }
        finally
        {
            _updatingProgress = false;
        }

        _playPauseButton.Content = _engine.IsPlaying ? "Pause" : "Play";
    }

    private void SetStatus(string status) => _statusText.Text = status;

    private static string FormatTime(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";

    private void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _tracksList.SelectedItem is not MusicTrack track)
        {
            return;
        }

        if (!_engine.IsPlaying)
        {
            SetStatus($"Ready: {track.DisplayName}");
        }
    }

    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_tracksList.SelectedIndex >= 0)
        {
            TryPlayFromIndex(_tracksList.SelectedIndex, direction: 1);
        }
    }

    private void OnPrevious(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_tracks.Count == 0)
        {
            return;
        }

        var index = _tracksList.SelectedIndex <= 0 ? _tracks.Count - 1 : _tracksList.SelectedIndex - 1;
        TryPlayFromIndex(index, direction: -1);
    }

    private void OnPlayPause(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_engine.IsPlaying)
        {
            _engine.Pause();
            _playPauseButton.Content = "Play";
            SetStatus("Paused");
            return;
        }

        if (_engine.IsPaused)
        {
            _engine.Resume();
            _playPauseButton.Content = "Pause";
            if (_tracksList.SelectedItem is MusicTrack current)
            {
                SetStatus($"Tuned: {current.DisplayName}");
            }

            return;
        }

        TryPlayFromIndex(Math.Max(0, _tracksList.SelectedIndex), direction: 1);
    }

    private void OnNext(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_tracks.Count == 0)
        {
            return;
        }

        var index = _tracksList.SelectedIndex < 0 ? 0 : (_tracksList.SelectedIndex + 1) % _tracks.Count;
        TryPlayFromIndex(index, direction: 1);
    }

    private void OnStop(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _engine.Stop();
        _playPauseButton.Content = "Play";
        UpdateProgress();
        SetStatus("Stopped");
    }

    private void OnOpenFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Directory.CreateDirectory(_musicDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _musicDirectory,
            UseShellExecute = true
        });
        LoadLibrary();
    }

    private void OnSeekCompleted(object? sender, PointerReleasedEventArgs e)
    {
        _seeking = false;
        _engine.Seek(TimeSpan.FromSeconds(_seekSlider.Value));
        UpdateProgress();
    }

    private void OnSeekSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updatingProgress || _seeking || !_engine.HasTrack)
        {
            return;
        }

        _engine.Seek(TimeSpan.FromSeconds(_seekSlider.Value));
        UpdateProgress();
    }

    private static Button Button(string text, double width, EventHandler<Avalonia.Interactivity.RoutedEventArgs> handler)
    {
        var button = new Button
        {
            Content = text,
            Width = width,
            Height = 30,
            Margin = new Thickness(6, 0, 0, 0)
        };
        button.Click += handler;
        return button;
    }

    private static TextBlock TextBlock(string color, double fontSize, string? text = null, FontWeight fontWeight = default) => new()
    {
        Text = text,
        Foreground = Brush(color),
        FontSize = fontSize,
        FontWeight = fontWeight == default ? FontWeight.Normal : fontWeight,
        TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock TimeText(string text, TextAlignment alignment) => new()
    {
        Text = text,
        Width = 42,
        Foreground = Brush("#bdb4d4"),
        FontSize = 12,
        TextAlignment = alignment,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
