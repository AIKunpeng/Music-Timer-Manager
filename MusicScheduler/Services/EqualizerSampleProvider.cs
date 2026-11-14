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
    /// <summary>
    /// 滤波器组。Read 在 WASAPI 音频线程调用，Update 在 UI 线程调用；
    /// 更新时整体替换数组引用（原子交换），音频线程取局部快照，
    /// 避免逐字段改写系数时读到新旧混合状态产生爆音。
    /// </summary>
    private volatile BiQuadFilter[] _bands;
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

    /// <summary>更新所有频段增益（dB）。整体构建新数组后原子交换引用。</summary>
    public void Update(EqualizerSettings settings)
    {
        if (settings is null) return;
        var newBands = new BiQuadFilter[_bands.Length];
        for (int i = 0; i < newBands.Length; i++)
        {
            newBands[i] = BiQuadFilter.PeakingEQ(_sampleRate, EqualizerSettings.CenterFrequencies[i], 1.0f, settings.GetBand(i));
        }
        _bands = newBands;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read <= 0) return read;

        var bands = _bands; // 局部快照：本帧内滤波器组不会中途被替换
        int frames = read / _channels;
        for (int f = 0; f < frames; f++)
        {
            for (int c = 0; c < _channels; c++)
            {
                float sample = buffer[offset + f * _channels + c];
                for (int b = 0; b < bands.Length; b++)
                {
                    sample = bands[b].Transform(sample);
                }
                buffer[offset + f * _channels + c] = sample;
            }
        }
        return read;
    }
}
