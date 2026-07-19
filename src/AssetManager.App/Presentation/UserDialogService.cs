using System.Windows;
using AssetManager.Application.Startup;

namespace AssetManager.App.Presentation;

public interface IUserDialogService
{
    bool Confirm(string message, string title);

    void ShowError(
        string message,
        string title = "AssetManager - エラー",
        Exception? exception = null);

    void ShowInformation(string message, string title = "AssetManager");
}

public sealed class WpfUserDialogService(IApplicationLogger? logger = null) : IUserDialogService
{
    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowError(
        string message,
        string title = "AssetManager - エラー",
        Exception? exception = null)
    {
        if (logger is not null)
        {
            _ = logger.LogErrorAsync(
                $"エラーダイアログを表示しました: {message}",
                exception ?? new InvalidOperationException(message));
        }

        _ = MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowInformation(string message, string title = "AssetManager")
    {
        _ = MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
