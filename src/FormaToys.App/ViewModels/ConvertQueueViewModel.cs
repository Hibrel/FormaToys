using System.Collections.ObjectModel;
using System.ComponentModel;
using FormaToys.App.Services;
using FormaToys.Core.Conversion;
using FormaToys.Core.Media;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FormaToys.App.ViewModels;

/// <summary>
/// 转换队列：唯一的事实来源。转换页按状态分成三段显示，队列页显示全量，
/// 两个页面绑定的是同一个实例（AppServices.Queue），所以状态永远同步。
/// </summary>
public sealed class ConvertQueueViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly DispatcherQueue _dispatcher;
    private readonly ObservableCollection<FileItemViewModel> AllItems = new();
    private readonly Dictionary<FileItemViewModel, CancellationTokenSource> _itemTokens = new();

    private CancellationTokenSource? _cts;
    private IReadOnlyList<FileItemViewModel> _batch = Array.Empty<FileItemViewModel>();
    private bool _isBusy;
    private double _overallProgress;
    private int _finishedCount;

    public ConvertQueueViewModel(DispatcherQueue dispatcher, AppServices services)
    {
        _dispatcher = dispatcher;
        _services = services;

        _qualityIndex = Math.Clamp(services.Settings.QualityIndex, 0, 2);
        _useCustomFolder = string.Equals(services.Settings.OutputMode, "custom", StringComparison.OrdinalIgnoreCase)
                           && !string.IsNullOrWhiteSpace(services.Settings.CustomOutputFolder);
        _customFolderText = services.Settings.CustomOutputFolder ?? string.Empty;
    }

    /// <summary>待转换：等待中、读取中、失败（可重试）、已取消。</summary>
    public ObservableCollection<FileItemViewModel> PendingItems { get; } = new();

    /// <summary>转换中：正在跑的任务，各自一条进度。</summary>
    public ObservableCollection<FileItemViewModel> RunningItems { get; } = new();

    /// <summary>已完成：能打开、定位、移除。</summary>
    public ObservableCollection<FileItemViewModel> CompletedItems { get; } = new();

    /// <summary>全部文件的原始顺序，三段只是它的三个视图。</summary>
    public IReadOnlyList<FileItemViewModel> Items => AllItems;

    public bool IsReady => _services.IsReady;

    public bool HasStartupError => !_services.IsReady;

    public string StartupErrorMessage => _services.StartupError ?? string.Empty;

    public bool IsBusy => _isBusy;

    #region 分段计数与可见性

    public int PendingCount => PendingItems.Count;

    public int RunningCount => RunningItems.Count;

    public int CompletedCount => CompletedItems.Count;

    public string PendingHeader => $"待转换 · {PendingCount}";

    public string RunningHeader => $"转换中 · {RunningCount}";

    public string CompletedHeader => $"已完成 · {CompletedCount}";

    public Visibility PendingVisibility => PendingCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RunningVisibility => RunningCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CompletedVisibility => CompletedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => AllItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ListVisibility => AllItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BusyVisibility => _isBusy ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region 批量作用域

    /// <summary>批量操作的作用域：指定类型的全部文件。</summary>
    public IReadOnlyList<FileItemViewModel> ScopeFor(MediaKind kind) =>
        AllItems.Where(i => i.Kind == kind).ToList();

    #endregion

    #region 汇总与进度

    public double OverallProgress => _overallProgress;

    public string BusyText => _batch.Count == 0
        ? string.Empty
        : $"{_finishedCount} / {_batch.Count} · {_overallProgress:0}%";

    public string SummaryText
    {
        get
        {
            if (AllItems.Count == 0)
            {
                return "暂无文件";
            }

            var total = AllItems.Sum(i => i.SizeBytes);
            var done = CompletedItems.Count;
            return done > 0
                ? $"{AllItems.Count} 个文件 · {DisplayFormat.Bytes(total)} · 已完成 {done}"
                : $"{AllItems.Count} 个文件 · {DisplayFormat.Bytes(total)}";
        }
    }

    public bool CanStart => !_isBusy && _services.IsReady && PendingItems.Any(IsConvertible);

    public string StartButtonText
    {
        get
        {
            if (_isBusy)
            {
                return "转换中";
            }

            var ready = PendingItems.Count(IsConvertible);
            if (ready > 0)
            {
                return $"开始转换 · {ready}";
            }

            return CompletedItems.Count > 0 ? "重新转换" : "开始转换";
        }
    }
    #endregion

    #region 质量与输出位置

    private int _qualityIndex;

    public int QualityIndex
    {
        get => _qualityIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, 2);
            if (!SetProperty(ref _qualityIndex, clamped))
            {
                return;
            }

            _services.Settings.QualityIndex = clamped;
            _services.SaveSettings();
            OnPropertyChanged(nameof(QualityText));
            OnPropertyChanged(nameof(IsQualitySmall));
            OnPropertyChanged(nameof(IsQualityBalanced));
            OnPropertyChanged(nameof(IsQualityHigh));
        }
    }

    public bool IsQualitySmall => _qualityIndex == 0;

    public bool IsQualityBalanced => _qualityIndex == 1;

    public bool IsQualityHigh => _qualityIndex == 2;

    public void SetQuality(int index) => QualityIndex = index;

    public string QualityText => _qualityIndex switch
    {
        0 => "文件小",
        2 => "高画质",
        _ => "均衡",
    };

    private bool _useCustomFolder;

    public bool UseCustomFolder
    {
        get => _useCustomFolder;
        set
        {
            if (!SetProperty(ref _useCustomFolder, value))
            {
                return;
            }

            _services.Settings.OutputMode = value ? "custom" : "source";
            _services.SaveSettings();
            RaiseOutputState();
        }
    }

    private string _customFolderText;

    public string CustomFolderText
    {
        get => _customFolderText;
        set
        {
            if (!SetProperty(ref _customFolderText, value ?? string.Empty))
            {
                return;
            }

            _services.Settings.CustomOutputFolder = value;
            _services.SaveSettings();
            RaiseOutputState();
        }
    }

    public string OutputSummaryText =>
        UseCustomFolder && !string.IsNullOrWhiteSpace(CustomFolderText)
            ? CompactPath(CustomFolderText)
            : "与源文件同目录";

    private void RaiseOutputState()
    {
        OnPropertyChanged(nameof(OutputSummaryText));
        OnPropertyChanged(nameof(NowOutputFolder));
    }

    /// <summary>当前全局输出目录；为空表示跟随源文件。</summary>
    public string? NowOutputFolder
    {
        get
        {
            if (!UseCustomFolder || string.IsNullOrWhiteSpace(CustomFolderText))
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(CustomFolderText);
                return CustomFolderText;
            }
            catch
            {
                return null;
            }
        }
    }

    private static string CompactPath(string path)
    {
        var trimmed = path.TrimEnd('\\');
        var name = System.IO.Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    #endregion

    #region 增删

    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        var changed = false;

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = System.IO.Path.GetFullPath(path);
            }
            catch
            {
                continue;
            }

            if (AllItems.Any(i => string.Equals(i.Path, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var item = new FileItemViewModel(fullPath, _dispatcher);
            item.StateChanged += OnItemStateChanged;
            item.FormatChanged += OnItemFormatChanged;

            AllItems.Add(item);
            PendingItems.Add(item);
            changed = true;
            RaiseSections();

            if (_services.Probe is null)
            {
                item.MarkFailed(_services.StartupError ?? "ffmpeg 不可用。");
                continue;
            }

            try
            {
                var info = await _services.Probe.ProbeAsync(fullPath).ConfigureAwait(true);
                item.ApplyInfo(info);
            }
            catch (Exception ex)
            {
                item.MarkFailed(ex.Message);
            }

            if (item.Kind != MediaKind.Unknown && item.SelectedFormat is null)
            {
                item.SelectedFormat = ResolveDefaultFormat(item.Kind);
            }
        }

        if (changed)
        {
            RaiseSections();
        }
    }

    public void RemoveItem(FileItemViewModel item)
    {
        if (item.State == ItemState.Running)
        {
            return;
        }

        Unhook(item);
        AllItems.Remove(item);
        PendingItems.Remove(item);
        RunningItems.Remove(item);
        CompletedItems.Remove(item);

        RaiseSections();
    }

    public void RemoveCompleted()
    {
        foreach (var item in CompletedItems.ToList())
        {
            Unhook(item);
            AllItems.Remove(item);
        }

        CompletedItems.Clear();
        RaiseSections();
    }

    public void RemovePending()
    {
        foreach (var item in PendingItems.ToList())
        {
            if (item.State == ItemState.Probing)
            {
                continue;
            }

            Unhook(item);
            AllItems.Remove(item);
        }

        for (var i = PendingItems.Count - 1; i >= 0; i--)
        {
            if (PendingItems[i].State != ItemState.Probing)
            {
                PendingItems.RemoveAt(i);
            }
        }

        RaiseSections();
    }

    /// <summary>把已完成的行放回待转换，用同样的设置再跑一遍。</summary>
    public void Requeue(FileItemViewModel item)
    {
        if (item.State != ItemState.Completed)
        {
            return;
        }

        item.Reset();
        RaiseSections();
    }

    public void RequeueCompleted()
    {
        foreach (var item in CompletedItems.ToList())
        {
            item.Reset();
        }

        RaiseSections();
    }

    #endregion

    #region 批量设置格式

    /// <summary>
    /// 按类型批量改目标格式：有勾选只作用于勾选的，否则作用于该类型的全部文件。
    /// </summary>
    public int ApplyFormatToKind(MediaKind kind, string formatId)
    {
        var format = FormatCatalog.Find(formatId);
        if (format is null)
        {
            return 0;
        }

        var scope = ScopeFor(kind);
        var applied = 0;
        foreach (var item in scope)
        {
            if (item.State is ItemState.Completed or ItemState.Running or ItemState.Probing)
            {
                continue;
            }

            item.SelectedFormat = format;
            applied++;
        }

        _services.Settings.LastFormatByKind[kind.ToString().ToLowerInvariant()] = format.Id;
        _services.SaveSettings();

        return applied;
    }

    #endregion

    #region 执行

    public async Task StartAsync()
    {
        if (_isBusy || !CanStart)
        {
            return;
        }

        var targets = PendingItems.Where(IsConvertible).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        _batch = targets;
        _finishedCount = 0;
        _overallProgress = 0;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isBusy = true;
        RaiseBusyState();

        var concurrency = Math.Clamp(_services.Settings.Concurrency, 1, 8);
        using var gate = new SemaphoreSlim(concurrency, concurrency);

        var tasks = targets.Select(async item =>
        {
            try
            {
                await gate.WaitAsync(token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await ConvertOneAsync(item, token).ConfigureAwait(true);
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // 单项异常已经在 ConvertOneAsync 里落成失败状态，这里只留诊断日志。
            LogException("批量转换", ex);
        }

        _isBusy = false;
        _cts?.Dispose();
        _cts = null;
        _batch = Array.Empty<FileItemViewModel>();
        _overallProgress = 0;
        _finishedCount = 0;

        RaiseBusyState();

        var last = CompletedItems.LastOrDefault();
        if (last?.OutputPath is not null && _services.Settings.OpenFolderWhenDone)
        {
            ShellService.RevealFile(last.OutputPath);
        }
    }

    private async Task ConvertOneAsync(FileItemViewModel item, CancellationToken token)
    {
        var format = item.SelectedFormat ?? FormatCatalog.DefaultFor(item.Kind);
        var folder = item.OutputDirectoryOverride ?? NowOutputFolder;
        var outputPath = OutputPlanner.Plan(item.Path, folder, format.Extension);

        var request = new ConversionRequest
        {
            InputPath = item.Path,
            FormatId = format.Id,
            Quality = QualityLevelExtensions.FromIndex(QualityIndex),
            OutputPath = outputPath,
            Duration = item.Info?.Duration ?? TimeSpan.Zero,
            InputKind = item.Kind,
        };

        using var itemCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _itemTokens[item] = itemCts;

        item.MarkRunning(outputPath);
        ConversionResult result;

        try
        {
            var progress = new Progress<ConversionProgress>(value =>
            {
                item.ApplyProgress(value);
                UpdateOverall();
            });

            result = await _services.Engine!.RunAsync(request, progress, itemCts.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            item.MarkFailed(ex.Message);
            FinishOne();
            return;
        }
        finally
        {
            _itemTokens.Remove(item);
        }

        if (result.Succeeded)
        {
            var size = 0L;
            try
            {
                size = new FileInfo(result.OutputPath).Length;
            }
            catch
            {
                // 拿不到大小不影响完成状态。
            }

            item.MarkCompleted(result.Elapsed, size);
        }
        else if (result.Canceled)
        {
            item.MarkCanceled();
        }
        else
        {
            item.MarkFailed(result.ErrorMessage ?? "未知错误。");
        }

        FinishOne();
    }

    private void FinishOne()
    {
        _finishedCount++;
        UpdateOverall();
    }

    private void UpdateOverall()
    {
        if (_batch.Count == 0)
        {
            return;
        }

        var sum = 0d;
        foreach (var item in _batch)
        {
            sum += item.State switch
            {
                ItemState.Completed => 100d,
                ItemState.Running => item.Progress,
                ItemState.Failed or ItemState.Canceled => 100d,
                _ => 0d,
            };
        }

        _overallProgress = Math.Clamp(sum / _batch.Count, 0d, 100d);
        OnPropertyChanged(nameof(OverallProgress));
        OnPropertyChanged(nameof(BusyText));
    }

    public void Cancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // 已经结束的 CTS 会抛，忽略。
        }
    }

    public void CancelItem(FileItemViewModel item)
    {
        if (_itemTokens.TryGetValue(item, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
                // 同上。
            }
        }
    }

    #endregion

    #region 内部

    private TargetFormat ResolveDefaultFormat(MediaKind kind)
    {
        var saved = _services.Settings.LastFormatByKind.GetValueOrDefault(kind.ToString().ToLowerInvariant());
        var format = FormatCatalog.Find(saved);
        return format is not null && format.Kind == kind ? format : FormatCatalog.DefaultFor(kind);
    }

    private static bool IsConvertible(FileItemViewModel item) =>
        item.Kind != MediaKind.Unknown
        && item.SelectedFormat is not null
        && item.State is ItemState.Pending or ItemState.Failed or ItemState.Canceled;

    private void Unhook(FileItemViewModel item)
    {
        item.StateChanged -= OnItemStateChanged;
        item.FormatChanged -= OnItemFormatChanged;
    }

    private void OnItemFormatChanged(object? sender, EventArgs e)
    {
        if (sender is not FileItemViewModel item || item.Kind == MediaKind.Unknown || item.SelectedFormat is null)
        {
            return;
        }

        _services.Settings.LastFormatByKind[item.Kind.ToString().ToLowerInvariant()] = item.SelectedFormat.Id;
        _services.SaveSettings();
        RaiseStartState();
    }

    private void OnItemStateChanged(object? sender, EventArgs e)
    {
        if (sender is not FileItemViewModel item)
        {
            return;
        }

        var target = SectionFor(item.Section);
        foreach (var section in new[] { PendingItems, RunningItems, CompletedItems })
        {
            if (!ReferenceEquals(section, target))
            {
                section.Remove(item);
            }
        }

        if (!target.Contains(item))
        {
            target.Add(item);
        }

        RaiseSections();
        RaiseStartState();
        UpdateOverall();
    }

    private ObservableCollection<FileItemViewModel> SectionFor(ItemSection section) => section switch
    {
        ItemSection.Running => RunningItems,
        ItemSection.Completed => CompletedItems,
        _ => PendingItems,
    };

    private void RaiseSections()
    {
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(PendingHeader));
        OnPropertyChanged(nameof(RunningHeader));
        OnPropertyChanged(nameof(CompletedHeader));
        OnPropertyChanged(nameof(PendingVisibility));
        OnPropertyChanged(nameof(RunningVisibility));
        OnPropertyChanged(nameof(CompletedVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(ListVisibility));
        OnPropertyChanged(nameof(SummaryText));
        RaiseStartState();
    }

    private void RaiseStartState()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(StartButtonText));
    }

    private void RaiseBusyState()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(BusyVisibility));
        OnPropertyChanged(nameof(BusyText));
        OnPropertyChanged(nameof(OverallProgress));
        RaiseStartState();
    }

    /// <summary>队列自身的意外异常写进 %LOCALAPPDATA%\FormaToys\queue.log，方便事后定位。</summary>
    internal static void LogException(string scope, Exception ex)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FormaToys");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                System.IO.Path.Combine(dir, "queue.log"),
                $"[{DateTime.Now:O}] {scope}: {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 日志失败不能影响转换。
        }
    }

    #endregion
}
