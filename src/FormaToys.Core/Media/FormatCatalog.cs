namespace FormaToys.Core.Media;

/// <summary>一个目标格式。</summary>
public sealed class TargetFormat
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>输出扩展名，带点，例如 ".mp4"。</summary>
    public required string Extension { get; init; }

    /// <summary>这个格式属于哪个输入类别（决定它出现在哪一组）。</summary>
    public required MediaKind Kind { get; init; }

    /// <summary>是否响应“质量”档位。</summary>
    public bool SupportsQuality { get; init; } = true;
}

/// <summary>
/// 目标格式清单。第一批只保留“几乎所有机器都能播”的格式，
/// 参数模板刻意保守，宁可画质普通也不要出现播不了的输出。
/// </summary>
public static class FormatCatalog
{
    private static readonly TargetFormat[] Formats =
    {
        new() { Id = "mp4", DisplayName = "MP4", Extension = ".mp4", Kind = MediaKind.Video },
        new() { Id = "mkv", DisplayName = "MKV", Extension = ".mkv", Kind = MediaKind.Video },
        new() { Id = "webm", DisplayName = "WebM", Extension = ".webm", Kind = MediaKind.Video },
        new() { Id = "gif", DisplayName = "GIF", Extension = ".gif", Kind = MediaKind.Video },

        new() { Id = "mp3", DisplayName = "MP3", Extension = ".mp3", Kind = MediaKind.Audio },
        new() { Id = "m4a", DisplayName = "M4A", Extension = ".m4a", Kind = MediaKind.Audio },
        new() { Id = "wav", DisplayName = "WAV", Extension = ".wav", Kind = MediaKind.Audio, SupportsQuality = false },
        new() { Id = "flac", DisplayName = "FLAC", Extension = ".flac", Kind = MediaKind.Audio },
        new() { Id = "ogg", DisplayName = "OGG", Extension = ".ogg", Kind = MediaKind.Audio },

        new() { Id = "jpg", DisplayName = "JPG", Extension = ".jpg", Kind = MediaKind.Image },
        new() { Id = "png", DisplayName = "PNG", Extension = ".png", Kind = MediaKind.Image },
        new() { Id = "webp", DisplayName = "WebP", Extension = ".webp", Kind = MediaKind.Image },
        new() { Id = "bmp", DisplayName = "BMP", Extension = ".bmp", Kind = MediaKind.Image, SupportsQuality = false },
    };

    public static IReadOnlyList<TargetFormat> All => Formats;

    public static IReadOnlyList<TargetFormat> For(MediaKind kind) =>
        Formats.Where(f => f.Kind == kind).ToArray();

    public static TargetFormat? Find(string? id) =>
        id is null ? null : Formats.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>某一类输入的默认目标格式。</summary>
    public static TargetFormat DefaultFor(MediaKind kind) => kind switch
    {
        MediaKind.Video => Find("mp4")!,
        MediaKind.Audio => Find("mp3")!,
        MediaKind.Image => Find("jpg")!,
        _ => Find("mp4")!,
    };

    public static string GroupHeader(MediaKind kind) => kind switch
    {
        MediaKind.Video => "视频",
        MediaKind.Audio => "音频",
        MediaKind.Image => "图片",
        _ => "其他",
    };
}
