using AssetManager.Application.Paths;
using Microsoft.Win32;

namespace AssetManager.App.Windows;

internal sealed class WpfWindowsPathPicker : IWindowsPathPicker
{
    public string? PickFile(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            CheckFileExists = true,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
