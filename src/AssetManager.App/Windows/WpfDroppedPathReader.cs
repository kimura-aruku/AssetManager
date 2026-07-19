using System.Windows;

namespace AssetManager.App.Windows;

internal static class WpfDroppedPathReader
{
    public static IReadOnlyList<string> Read(IDataObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.GetDataPresent(DataFormats.FileDrop)
            ? (data.GetData(DataFormats.FileDrop) as string[] ?? [])
            : [];
    }
}
