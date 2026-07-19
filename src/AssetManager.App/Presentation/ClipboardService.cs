using System.Windows;

namespace AssetManager.App.Presentation;

public interface IClipboardService
{
    bool ContainsText();

    string GetText();

    void SetText(string text);
}

public sealed class WpfClipboardService : IClipboardService
{
    public bool ContainsText()
    {
        return Clipboard.ContainsText(TextDataFormat.UnicodeText);
    }

    public string GetText()
    {
        return Clipboard.GetText(TextDataFormat.UnicodeText);
    }

    public void SetText(string text)
    {
        Clipboard.SetText(text, TextDataFormat.UnicodeText);
    }
}
