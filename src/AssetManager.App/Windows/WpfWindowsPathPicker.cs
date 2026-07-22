using System.IO;
using AssetManager.Application.Paths;
using Microsoft.Win32;

namespace AssetManager.App.Windows;

internal sealed class WpfWindowsPathPicker : IWindowsPathPicker
{
    private const string FolderSelectionPlaceholder = "このフォルダーを選択";

    public string? PickFileOrFolder(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false,
            Multiselect = false,
            FileName = FolderSelectionPlaceholder,
            Filter = "すべてのファイル (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        if (File.Exists(dialog.FileName) || Directory.Exists(dialog.FileName))
        {
            return dialog.FileName;
        }

        return string.Equals(
                Path.GetFileName(dialog.FileName),
                FolderSelectionPlaceholder,
                StringComparison.Ordinal)
            ? Path.GetDirectoryName(dialog.FileName)
            : dialog.FileName;
    }

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
