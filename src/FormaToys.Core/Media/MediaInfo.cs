namespace FormaToys.Core.Media;

/// <summary>ffprobe 探测出来的输入文件信息（只保留界面与命令行需要的那部分）。</summary>
public sealed class MediaInfo
{
    public required string Path { get; init; }

    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    public TimeSpan Duration { get; init; }

    public string? VideoCodec { get; init; }

    public string? AudioCodec { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public double Fps { get; init; }

    public long SizeBytes { get; init; }

    public string? Container { get; init; }

    public int AudioChannels { get; init; }

    public int AudioSampleRate { get; init; }

    public bool HasVideo => Kind == MediaKind.Video;

    public bool HasAudio => AudioCodec is not null;

    public string FileName => System.IO.Path.GetFileName(Path);
}
