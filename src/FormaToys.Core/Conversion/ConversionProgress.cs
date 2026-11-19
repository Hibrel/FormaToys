namespace FormaToys.Core.Conversion;

/// <summary>一次转换的进度快照。Total 未知时（图片、直播流、坏文件）界面应回退到不确定态。</summary>
public readonly struct ConversionProgress
{
    public ConversionProgress(double ratio, TimeSpan elapsed, TimeSpan total)
    {
        Ratio = ratio;
        Elapsed = elapsed;
        Total = total;
    }

    /// <summary>0..1，只在 Total 已知时有意义。</summary>
    public double Ratio { get; }

    /// <summary>ffmpeg 已经处理到的时间点。</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>总时长；未知时为零。</summary>
    public TimeSpan Total { get; }

    /// <summary>总时长可用（超过 50ms 才认为有意义，避免单帧图片抖动）。</summary>
    public bool IsDeterminate => Total > TimeSpan.FromMilliseconds(50);
}
