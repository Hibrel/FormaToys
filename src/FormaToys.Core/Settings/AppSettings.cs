using System.Text.Json;

namespace FormaToys.Core.Settings;

public sealed class AppSettings
{
    /// <summary>"source" = 与源文件同目录；"custom" = 指定目录。</summary>
    public string OutputMode { get; set; } = "source";

    public string? CustomOutputFolder { get; set; }

    /// <summary>0 = 文件小，1 = 均衡，2 = 高画质。</summary>
    public int QualityIndex { get; set; } = 1;

    public int Concurrency { get; set; } = 1;

    public bool OpenFolderWhenDone { get; set; }

    /// <summary>每类输入上次使用的目标格式，例如 video → "mp4"。</summary>
    public Dictionary<string, string> LastFormatByKind { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? LastOutputFolder { get; set; }

    public string? EffectiveOutputFolder() =>
        string.Equals(OutputMode, "custom", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(CustomOutputFolder)
            ? CustomOutputFolder
            : null;
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FormaToys",
            "settings.json");
    }

    public string FilePath => _path;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // 配置坏了就当没配过，别让程序起不来。
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // 写配置失败不影响转换。
        }
    }
}
