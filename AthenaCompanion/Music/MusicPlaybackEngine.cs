using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AthenaCompanion.Music;

internal sealed class MusicPlaybackEngine : IDisposable
{
    private Process? _process;
    private string? _currentTrack;
    private DateTimeOffset _startedAt;
    private TimeSpan _pausedAt;
    private bool _isPaused;
    private bool _stopping;

    public event EventHandler? PlaybackStopped;

    public bool HasTrack => _currentTrack is not null;
    public bool IsPlaying => _process is { HasExited: false } && !_isPaused;
    public bool IsPaused => _isPaused;
    public TimeSpan Duration => TimeSpan.Zero;
    public TimeSpan Position => IsPlaying ? DateTimeOffset.UtcNow - _startedAt : _pausedAt;

    public void Play(string filePath)
    {
        CloseCurrent();
        StartProcess(filePath);
        _currentTrack = filePath;
        _startedAt = DateTimeOffset.UtcNow;
        _pausedAt = TimeSpan.Zero;
        _isPaused = false;
    }

    public void Resume()
    {
        if (!_isPaused || string.IsNullOrWhiteSpace(_currentTrack))
        {
            return;
        }

        StartProcess(_currentTrack);
        _startedAt = DateTimeOffset.UtcNow - _pausedAt;
        _isPaused = false;
    }

    public void Pause()
    {
        if (!IsPlaying)
        {
            return;
        }

        _pausedAt = Position;
        StopProcess(suppressEvent: true);
        _isPaused = true;
    }

    public void Stop() => CloseCurrent();

    public void Seek(TimeSpan position)
    {
        // The native macOS afplay bridge does not expose precise seeking.
        _pausedAt = position < TimeSpan.Zero ? TimeSpan.Zero : position;
    }

    public void Close() => CloseCurrent();

    public void Dispose() => CloseCurrent();

    private void StartProcess(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Music file not found.", filePath);
        }

        var startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? new ProcessStartInfo("/usr/bin/afplay", QuoteArgument(filePath)) { UseShellExecute = false }
            : new ProcessStartInfo(filePath) { UseShellExecute = true };

        var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start native audio playback.");
        process.EnableRaisingEvents = true;
        process.Exited += OnProcessExited;
        _process = process;
    }

    private void CloseCurrent()
    {
        StopProcess(suppressEvent: true);
        _currentTrack = null;
        _pausedAt = TimeSpan.Zero;
        _isPaused = false;
    }

    private void StopProcess(bool suppressEvent)
    {
        var process = _process;
        if (process is null)
        {
            return;
        }

        _process = null;
        process.Exited -= OnProcessExited;
        _stopping = suppressEvent;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort while closing a native player process.
        }
        finally
        {
            process.Dispose();
            _stopping = false;
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (sender is Process process)
        {
            process.Exited -= OnProcessExited;
            process.Dispose();
        }

        _process = null;
        _isPaused = false;
        _pausedAt = TimeSpan.Zero;
        if (!_stopping)
        {
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string QuoteArgument(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
