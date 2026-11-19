using FormaToys.App.Services;
using FormaToys.App.ViewModels;
using FormaToys.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace FormaToys.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Unloaded += (_, _) => AppServices.Current.SaveSettings();
    }

    public AppSettings Settings => AppServices.Current.Settings;

    public ConvertQueueViewModel Queue => AppServices.Current.Queue;

    /// <summary>并发数是 1 起始的，ComboBox 索引是 0 起始的。</summary>
    public int ConcurrencyIndex
    {
        get => Math.Clamp(Settings.Concurrency, 1, 4) - 1;
        set
        {
            Settings.Concurrency = Math.Clamp(value + 1, 1, 4);
            AppServices.Current.SaveSettings();
        }
    }

    public string FfmpegText
    {
        get
        {
            var info = AppServices.Current.Ffmpeg;
            if (info is null)
            {
                return "ffmpeg：未找到（" + (AppServices.Current.StartupError ?? "未知原因") + "）";
            }

            return $"ffmpeg {info.ShortVersion} · {(info.IsGpl ? "GPL 构建" : "非 GPL 构建")}";
        }
    }

    public string EnginePathText => AppServices.Current.Tools?.FfmpegPath ?? "未解析到 ffmpeg 路径";

    public string SettingsPathText => AppServices.Current.Store.FilePath;

    /// <summary>质量档位：0 文件小 / 1 均衡 / 2 高画质。</summary>
    public int QualityIndex
    {
        get => Queue.QualityIndex;
        set => Queue.SetQuality(value);
    }

    private async void OnBrowseOutputClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeFilter.Add("*");

            if (App.MainWindow is { } window)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(
                    picker,
                    WinRT.Interop.WindowNative.GetWindowHandle(window));
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                Queue.UseCustomFolder = true;
                Queue.CustomFolderText = folder.Path;
            }
        }
        catch
        {
            // 取消选择不需要提示。
        }
    }

    private void OnOpenSettingsFolderClick(object sender, RoutedEventArgs e)
    {
        var folder = Path.GetDirectoryName(AppServices.Current.Store.FilePath);
        if (!string.IsNullOrEmpty(folder))
        {
            ShellService.OpenFolder(folder);
        }
    }

    private void OnOpenProgramFolderClick(object sender, RoutedEventArgs e) =>
        ShellService.OpenFolder(AppContext.BaseDirectory);
}
