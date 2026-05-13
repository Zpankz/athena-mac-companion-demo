namespace AthenaCompanion.Voice;

internal sealed class AthenaAudioOutput : IDisposable
{
    public void Start()
    {
    }

    public void AddPcm16(byte[] audio)
    {
        if (audio.Length == 0)
        {
            return;
        }

        // A native macOS PCM sink is intentionally isolated as a follow-up.
        // Dropping audio is preferable to compiling against Windows WinMM.
    }

    public void Clear()
    {
    }

    public void Dispose()
    {
    }
}
