using System.Diagnostics;
using System.Runtime.Versioning;
using AssetManager.Application.Paths;

namespace AssetManager.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsShellService : IWindowsShellService
{
    public void Open(string path)
    {
        var normalized = WindowsPathNormalizer.NormalizeForStorage(path);
        EnsureExists(normalized);
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = normalized,
            UseShellExecute = true,
        });
    }

    public void ShowInExplorer(string path, PathEntryKind kind)
    {
        var normalized = WindowsPathNormalizer.NormalizeForStorage(path);
        EnsureExists(normalized);
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true,
        };
        if (kind == PathEntryKind.File)
        {
            startInfo.Arguments = $"/select,\"{normalized}\"";
        }
        else
        {
            startInfo.ArgumentList.Add(normalized);
        }

        _ = Process.Start(startInfo);
    }

    private static void EnsureExists(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("指定されたパスが見つかりません。", path);
        }
    }
}
