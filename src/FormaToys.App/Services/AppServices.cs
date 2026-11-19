using FormaToys.App.ViewModels;
using FormaToys.Core.Conversion;
using FormaToys.Core.Media;
using FormaToys.Core.Settings;
using FormaToys.Core.Tooling;
using Microsoft.UI.Dispatching;

namespace FormaToys.App.Services;

/// <summary>
/// 进程级的服务集合。工具链解析失败不抛异常，而是把原因留在 StartupError 里，
/// 让界面可以正常打开并提示用户，而不是直接闪退。
/// </summary>
public sealed class AppServices
{
    private AppServices(SettingsStore store, AppSettings settings)
    {
        Store = store;
        Settings = settings;
    }

    public static AppServices Current { get; private set; } = null!;

    public SettingsStore Store { get; }

    public AppSettings Settings { get; }

    public ToolPaths? Tools { get; private set; }

    public MediaProbe? Probe { get; private set; }

    public ConversionEngine? Engine { get; private set; }

    public FfmpegInfo? Ffmpeg { get; private set; }

    /// <summary>转换队列的唯一实例，转换页和队列页共享它。</summary>
    public ConvertQueueViewModel Queue { get; private set; } = null!;

    public string? StartupError { get; private set; }

    public bool IsReady => Engine is not null && Probe is not null;

    public static void Initialize()
    {
        var store = new SettingsStore();
        var settings = store.Load();
        var services = new AppServices(store, settings);

        try
        {
            services.Tools = ToolPaths.Resolve();
            services.Engine = new ConversionEngine(services.Tools);
            services.Probe = new MediaProbe(services.Tools);
            services.Ffmpeg = StartupCheck.Run(services.Tools);
        }
        catch (Exception ex)
        {
            services.StartupError = ex.Message;
        }

        services.Queue = new ConvertQueueViewModel(DispatcherQueue.GetForCurrentThread(), services);

        Current = services;
    }

    public void SaveSettings() => Store.Save(Settings);
}
