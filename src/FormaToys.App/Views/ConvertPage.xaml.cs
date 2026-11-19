using FormaToys.App.Services;
using FormaToys.App.ViewModels;
using FormaToys.Core.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace FormaToys.App.Views;

public sealed partial class ConvertPage : Page
{
    private static readonly string[] InputExtensions =
    {
        ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv", ".flv", ".ts", ".mts", ".gif",
        ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".opus", ".wma", ".aiff",
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff",
    };

    private bool _loaded;

    public ConvertPage()
    {
        Vm = AppServices.Current.Queue;
        InitializeComponent();
    }

    public ConvertQueueViewModel Vm { get; }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        BuildBatchFormatMenu();

        if (App.StartupFiles.Count > 0)
        {
            _ = Vm.AddFilesAsync(App.StartupFiles);
        }
    }

    /// <summary>“批量设置格式”下拉：按 视频 / 音频 / 图片 分组，每组列出该类的目标格式。</summary>
    private void BuildBatchFormatMenu()
    {
        BatchFormatMenu.Items.Clear();

        foreach (var kind in new[] { MediaKind.Video, MediaKind.Audio, MediaKind.Image })
        {
            var sub = new MenuFlyoutSubItem { Text = $"所有{FormatCatalog.GroupHeader(kind)}" };

            foreach (var format in FormatCatalog.For(kind))
            {
                var item = new MenuFlyoutItem
                {
                    Text = $"→ {format.DisplayName}",
                    Tag = $"{kind}|{format.Id}",
                };
                item.Click += OnBatchFormatClick;
                sub.Items.Add(item);
            }

            BatchFormatMenu.Items.Add(sub);
        }
    }

    private void OnBatchFormatClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string tag })
        {
            return;
        }

        var parts = tag.Split('|');
        if (parts.Length != 2 || !Enum.TryParse<MediaKind>(parts[0], out var kind))
        {
            return;
        }

        Vm.ApplyFormatToKind(kind, parts[1]);
    }

    private async void OnStartClick(object sender, RoutedEventArgs e) => await Vm.StartAsync();

    private void OnCancelClick(object sender, RoutedEventArgs e) => Vm.Cancel();

    private void OnClearCompletedClick(object sender, RoutedEventArgs e) => Vm.RemoveCompleted();

    private void OnRequeueCompletedClick(object sender, RoutedEventArgs e) => Vm.RequeueCompleted();

    #region 拖放

    private void OnPageDragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "添加到转换列表";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;
    }

    private async void OnPageDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var files = items
                .OfType<Windows.Storage.StorageFile>()
                .Select(f => f.Path)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (files.Length > 0)
            {
                await Vm.AddFilesAsync(files);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    #endregion

    private async void OnBrowseFilesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.Downloads,
            };

            foreach (var extension in InputExtensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            if (App.MainWindow is { } window)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(
                    picker,
                    WinRT.Interop.WindowNative.GetWindowHandle(window));
            }

            var files = await picker.PickMultipleFilesAsync();
            if (files is { Count: > 0 })
            {
                await Vm.AddFilesAsync(files.Select(f => f.Path));
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("选择文件失败", ex.Message);
        }
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "关闭",
            XamlRoot = XamlRoot,
        };

        await dialog.ShowAsync();
    }
}
