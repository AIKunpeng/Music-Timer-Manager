using System;
using MusicScheduler.Models;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicScheduler.Services;

/// <summary>
/// 10 段图形均衡器 SampleProvider。
/// 信号链：Reader → ... → EqualizerSampleProvider → ...
/// </summary>
public class EqualizerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly BiQuadFilter[] _bands;
    private readonly int _channels;
    private readonly float _sampleRate;

    public WaveFormat WaveFormat { get; }

    public EqualizerSampleProvider(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _channels = source.WaveFormat.Channels;
        _sampleRate = source.WaveFormat.SampleRate;
        WaveFormat = source.WaveFormat;
        _bands = new BiQuadFilter[EqualizerSettings.BandCount];
        for (int i = 0; i < _bands.Length; i++)
        {
            _bands[i] = BiQuadFilter.PeakingEQ(_sampleRate, EqualizerSettings.CenterFrequencies[i], 1.0f, 0f);
        }
    }

    /// <summary>更新所有频段增益（dB）。</summary>
    public void Update(EqualizerSettings settings)
    {
        if (settings is null) return;
        for (int i = 0; i < _bands.Length; i++)
        {
            _bands[i].SetPeakingEq(_sampleRate, EqualizerSettings.CenterFrequencies[i], 1.0f, settings.GetBand(i));
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read <= 0) return read;

        int frames = read / _channels;
        for (int f = 0; f < frames; f++)
        {
            for (int c = 0; c < _channels; c++)
            {
                float sample = buffer[offset + f * _channels + c];
                for (int b = 0; b < _bands.Length; b++)
                {
                    sample = _bands[b].Transform(sample);
                }
                buffer[offset + f * _channels + c] = sample;
            }
        }
        return read;
    }
}
