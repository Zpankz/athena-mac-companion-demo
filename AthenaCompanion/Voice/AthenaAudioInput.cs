namespace AthenaCompanion.Voice;

internal sealed class AthenaAudioInput : IDisposable
{
    public const int SampleRate = 24000;

    public event EventHandler<byte[]>? AudioAvailable;

    public void Start() =>
        throw new PlatformNotSupportedException(
            "Realtime microphone capture has not been wired to a native macOS audio backend yet.");

    public void Stop()
    {
    }

    public void Dispose() => Stop();

    internal void PublishForTest(byte[] pcm16) => AudioAvailable?.Invoke(this, pcm16);
}
