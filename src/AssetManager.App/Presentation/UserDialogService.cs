using System.Windows;

namespace AssetManager.App.Presentation;

public interface IUserDialogService
{
    bool Confirm(string message, string title);

    void ShowError(string message, string title = "AssetManager - エラー");

    void ShowInformation(string message, string title = "AssetManager");
}

public sealed class WpfUserDialogService : IUserDialogService
{
    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowError(string message, string title = "AssetManager - エラー")
    {
        _ = MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowInformation(string message, string title = "AssetManager")
    {
        _ = MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
