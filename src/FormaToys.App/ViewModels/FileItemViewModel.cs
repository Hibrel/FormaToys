using FormaToys.Core.Conversion;
using FormaToys.Core.Media;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FormaToys.App.ViewModels;

public enum ItemState
{
    Probing,
    Pending,
    Running,
    Completed,
    Failed,
    Canceled,
}

/// <summary>列表分成三段：待转换 / 转换中 / 已完成。</summary>
public enum ItemSection
{
    Pending,
    Running,
    Completed,
}

/// <summary>
/// 列表里的一行：一个文件 + 它自己的目标格式 + 它自己的输出位置。
/// 格式是“跟着文件走”的，所以这些状态必须挂在行上，而不是页面级。
/// </summary>
public sealed class FileItemViewModel : ObservableObject
{
    private static readonly IReadOnlyList<TargetFormat> VideoFormats = FormatCatalog.For(MediaKind.Video);
    private static readonly IReadOnlyList<TargetFormat> AudioFormats = FormatCatalog.For(MediaKind.Audio);
    private static readonly IReadOnlyList<TargetFormat> ImageFormats = FormatCatalog.For(MediaKind.Image);
    private static readonly IReadOnlyList<TargetFormat> NoFormats = Array.Empty<TargetFormat>();

    private readonly DispatcherQueue _dispatcher;

    private MediaInfo? _info;
    private ItemState _state = ItemState.Probing;
    private double _progress;
    private bool _isIndeterminate;
    private string _progressText = string.Empty;
    private string _statusText = "读取中";
    private string _metaText = string.Empty;
    private string? _outputPath;
    private string? _outputDirectoryOverride;
    private string? _error;
    private TimeSpan _elapsed;
    private long _outputSize;
    private TargetFormat? _selectedFormat;

    public FileItemViewModel(string path, DispatcherQueue dispatcher)
    {
        Path = path;
        _dispatcher = dispatcher;
        FileName = System.IO.Path.GetFileName(path);
    }

    /// <summary>行状态迁移（Waiting → Running → Completed 等）时触发，队列据此把行搬到对应分区。</summary>
    public event EventHandler? StateChanged;

    /// <summary>用户在行内改了目标格式。</summary>
    public event EventHandler? FormatChanged;

    public string Path { get; }

    public string FileName { get; }

    public MediaInfo? Info => _info;

    public MediaKind Kind => _info?.Kind ?? MediaKind.Unknown;

    public long SizeBytes => _info?.SizeBytes ?? 0;

    public ItemState State => _state;

    public ItemSection Section => _state switch
    {
        ItemState.Running => ItemSection.Running,
        ItemState.Completed => ItemSection.Completed,
        _ => ItemSection.Pending,
    };

    public bool IsFinished => _state is ItemState.Completed or ItemState.Failed or ItemState.Canceled;

    #region 目标格式（每行独立）

    public IReadOnlyList<TargetFormat> Formats => Kind switch
    {
        MediaKind.Video => VideoFormats,
        MediaKind.Audio => AudioFormats,
        MediaKind.Image => ImageFormats,
        _ => NoFormats,
    };

    public TargetFormat? SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (ReferenceEquals(_selectedFormat, value) || value is null)
            {
                return;
            }

            _selectedFormat = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedFormatName));
            OnPropertyChanged(nameof(TargetText));
            FormatChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string SelectedFormatName => _selectedFormat?.DisplayName ?? "—";

    /// <summary>“MP4 ← 源 MP4”，已完成行显示为“→ MP4 · 2.2 MB”。</summary>
    public string TargetText => _selectedFormat is null
        ? string.Empty
        : _state == ItemState.Completed
            ? $"→ {_selectedFormat.DisplayName} · {DisplayFormat.Bytes(_outputSize)}"
            : _selectedFormat.DisplayName;

    #endregion

    #region 输出位置

    /// <summary>行级覆盖的输出目录；为空表示跟随全局设置。</summary>
    public string? OutputDirectoryOverride => _outputDirectoryOverride;

    public void SetOutputDirectoryOverride(string? directory)
    {
        _outputDirectoryOverride = string.IsNullOrWhiteSpace(directory) ? null : directory;
        OnPropertyChanged(nameof(OutputDirectoryOverride));
        OnPropertyChanged(nameof(HasOutputOverride));
        OnPropertyChanged(nameof(OutputLocationText));
    }

    public bool HasOutputOverride => _outputDirectoryOverride is not null;

    public string OutputLocationText => _outputDirectoryOverride ?? "与源文件同目录";

    public string? OutputPath => _outputPath;

    public long OutputSizeBytes => _outputSize;

    public TimeSpan Elapsed => _elapsed;

    public string? Error => _error;

    #endregion

    #region 展示

    public double Progress => _progress;

    public bool IsIndeterminate => _isIndeterminate;

    public string ProgressText => _progressText;

    public string StatusText => _statusText;

    public string MetaText => _metaText;

    public bool HasError => _error is not null;

    public string KindGlyph => Kind switch
    {
        MediaKind.Video => "\uE714",
        MediaKind.Audio => "\uE8D6",
        MediaKind.Image => "\uE91B",
        _ => "\uE7C3",
    };

    public string KindLabel => Kind switch
    {
        MediaKind.Video => "视频",
        MediaKind.Audio => "音频",
        MediaKind.Image => "图片",
        _ => "未知",
    };

    public Visibility ProgressVisibility => _state == ItemState.Running ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FormatPickerVisibility =>
        _state is ItemState.Pending or ItemState.Failed or ItemState.Canceled ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RunningFormatVisibility => _state == ItemState.Running ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DoneTextVisibility => _state == ItemState.Completed ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OpenVisibility => _state == ItemState.Completed ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RevealVisibility =>
        _state == ItemState.Completed && _outputPath is not null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RemoveVisibility => _state == ItemState.Running ? Visibility.Collapsed : Visibility.Visible;

    public Visibility CancelVisibility => _state == ItemState.Running ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FailedStripeVisibility => _state == ItemState.Failed ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RunningStripeVisibility => _state == ItemState.Running ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DoneStripeVisibility => _state == ItemState.Completed ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CanceledStripeVisibility => _state == ItemState.Canceled ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region 状态迁移

    public void ApplyInfo(MediaInfo info)
    {
        _info = info;
        _metaText = DisplayFormat.Meta(info);
        if (info.Kind == MediaKind.Unknown)
        {
            _state = ItemState.Failed;
            _statusText = "无法识别";
            _error = "无法解析该文件，可能已损坏或不是受支持的媒体格式。";
        }
        else
        {
            _state = ItemState.Pending;
            _statusText = "等待";
        }

        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(KindGlyph));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(Formats));
        OnPropertyChanged(nameof(SizeBytes));
        RaiseMeta();
        PublishState();
    }

    public void MarkRunning(string outputPath)
    {
        _outputPath = outputPath;
        _state = ItemState.Running;
        _progress = 0;
        _isIndeterminate = false;
        _progressText = string.Empty;
        _statusText = "转换中";
        _error = null;
        RaiseMeta();
        PublishState();
    }

    public void ApplyProgress(ConversionProgress progress)
    {
        void Apply()
        {
            _isIndeterminate = !progress.IsDeterminate;
            _progress = Math.Clamp(progress.Ratio * 100d, 0d, 100d);

            var elapsed = DisplayFormat.Duration(progress.Elapsed);
            if (progress.IsDeterminate)
            {
                var total = DisplayFormat.Duration(progress.Total);
                _progressText = string.IsNullOrEmpty(elapsed)
                    ? $"{_progress:0}%"
                    : $"{_progress:0}% · {elapsed} / {total}";
            }
            else
            {
                _progressText = string.IsNullOrEmpty(elapsed) ? "处理中" : elapsed;
            }

            // 进度百分比放在进度条旁边，状态列只写活动，避免同一行出现两个 11%。
            _statusText = "转换中";

            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(IsIndeterminate));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(StatusText));
        }

        Dispatch(Apply);
    }

    public void MarkCompleted(TimeSpan elapsed, long outputSize)
    {
        _state = ItemState.Completed;
        _progress = 100;
        _isIndeterminate = false;
        _elapsed = elapsed;
        _outputSize = outputSize;
        _statusText = "完成";
        _progressText = string.Empty;
        _error = null;
        RaiseMeta();
        OnPropertyChanged(nameof(TargetText));
        PublishState();
    }

    public void MarkFailed(string message)
    {
        _state = ItemState.Failed;
        _isIndeterminate = false;
        _progress = 0;
        _progressText = string.Empty;
        _error = message;
        _statusText = "失败";
        _metaText = Compact(message);
        RaiseMeta();
        PublishState();
    }

    public void MarkCanceled()
    {
        _state = ItemState.Canceled;
        _isIndeterminate = false;
        _progress = 0;
        _progressText = string.Empty;
        _statusText = "已取消";
        RaiseMeta();
        PublishState();
    }

    public void Reset()
    {
        if (_state is ItemState.Running or ItemState.Probing)
        {
            return;
        }

        _state = _info is null || _info.Kind == MediaKind.Unknown ? ItemState.Failed : ItemState.Pending;
        _statusText = _info is null ? "读取中" : "等待";
        _progress = 0;
        _isIndeterminate = false;
        _progressText = string.Empty;
        _error = null;
        _outputPath = null;
        _outputSize = 0;
        _elapsed = TimeSpan.Zero;
        RaiseMeta();
        OnPropertyChanged(nameof(TargetText));
        PublishState();
    }

    #endregion

    /// <summary>把 ffmpeg 的多行 stderr 压成一行可显示的原因。</summary>
    private static string Compact(string message)
    {
        var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var line = lines.Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "转换失败。";
        line = line.Replace('\t', ' ').Trim();
        return line.Length <= 160 ? line : line[..157] + "…";
    }

    private void RaiseMeta()
    {
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(OutputPath));
    }

    private void PublishState()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Section));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressVisibility));
        OnPropertyChanged(nameof(FormatPickerVisibility));
        OnPropertyChanged(nameof(RunningFormatVisibility));
        OnPropertyChanged(nameof(DoneTextVisibility));
        OnPropertyChanged(nameof(OpenVisibility));
        OnPropertyChanged(nameof(RevealVisibility));
        OnPropertyChanged(nameof(RemoveVisibility));
        OnPropertyChanged(nameof(CancelVisibility));
        OnPropertyChanged(nameof(FailedStripeVisibility));
        OnPropertyChanged(nameof(RunningStripeVisibility));
        OnPropertyChanged(nameof(DoneStripeVisibility));
        OnPropertyChanged(nameof(CanceledStripeVisibility));
        OnPropertyChanged(nameof(TargetText));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Dispatch(Action action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcher.TryEnqueue(() => action());
        }
    }
}
