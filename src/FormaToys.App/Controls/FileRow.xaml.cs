using System.Diagnostics;
using FormaToys.App.Services;
using FormaToys.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace FormaToys.App.Controls;

/// <summary>
/// 列表里的一行。数据来自 DataContext 上的 <see cref="FileItemViewModel"/>，
/// 所有动作都转交给共享的 <see cref="ConvertQueueViewModel"/>。
/// </summary>
public sealed partial class FileRow : UserControl
{
    public FileRow()
    {
        InitializeComponent();
    }

    private FileItemViewModel? Item => DataContext as FileItemViewModel;

    private static ConvertQueueViewModel? Queue => AppServices.Current.Queue;

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        if (Item?.OutputPath is { } path && File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                // 没有关联程序时静默失败，用户可以改用“在资源管理器中显示”。
            }
        }
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (Item?.OutputPath is { } path)
        {
            ShellService.RevealFile(path);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (Item is { } item)
        {
            Queue?.CancelItem(item);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (Item is { } item)
        {
            Queue?.RemoveItem(item);
        }
    }

    private void OnRequeueClick(object sender, RoutedEventArgs e)
    {
        if (Item is { } item)
        {
            Queue?.Requeue(item);
        }
    }

    private void OnResetFolderClick(object sender, RoutedEventArgs e) => Item?.SetOutputDirectoryOverride(null);

    private async void OnPickFolderClick(object sender, RoutedEventArgs e)
    {
        var item = Item;
        if (item is null)
        {
            return;
        }

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
                item.SetOutputDirectoryOverride(folder.Path);
            }
        }
        catch
        {
            // 取消或初始化失败都不需要打断用户。
        }
    }
}
