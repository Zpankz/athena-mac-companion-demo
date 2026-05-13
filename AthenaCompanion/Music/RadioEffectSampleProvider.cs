namespace AthenaCompanion.Music;

internal readonly record struct AudioSampleFormat(int SampleRate, int Channels);

internal interface IFloatSampleProvider
{
    AudioSampleFormat Format { get; }

    int Read(float[] buffer, int offset, int count);
}

internal sealed class RadioEffectSampleProvider : IFloatSampleProvider
{
    public const int OutputSampleRate = 24000;

    private readonly IFloatSampleProvider _source;
    private readonly Random _random;
    private float _last;

    public RadioEffectSampleProvider(IFloatSampleProvider source, Random? random = null)
    {
        _source = source;
        _random = random ?? new Random();
    }

    public AudioSampleFormat Format { get; } = new(OutputSampleRate, 1);

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        var channels = Math.Max(1, _source.Format.Channels);
        var write = offset;

        for (var i = offset; i < offset + read; i += channels)
        {
            var mono = 0f;
            for (var channel = 0; channel < channels && i + channel < offset + read; channel++)
            {
                mono += buffer[i + channel];
            }

            mono /= channels;
            var noise = ((float)_random.NextDouble() - 0.5f) * 0.035f;
            var filtered = _last * 0.74f + mono * 0.26f + noise;
            _last = filtered;
            buffer[write++] = Math.Clamp(filtered * 1.18f, -1f, 1f);
        }

        return write - offset;
    }
}
