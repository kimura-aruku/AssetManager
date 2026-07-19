using AssetManager.Application.Paths;

namespace AssetManager.Infrastructure.Windows;

public sealed class PhysicalWindowsPathFileSystem : IWindowsPathFileSystem
{
    public DriveType GetDriveType(string driveRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driveRoot);
        return new DriveInfo(driveRoot).DriveType;
    }

    public PathEntryKind? GetExistingKind(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (File.Exists(path))
        {
            return PathEntryKind.File;
        }

        return Directory.Exists(path) ? PathEntryKind.Folder : null;
    }

    public PathCheckResult Check(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            _ = File.GetAttributes(path);
            return new PathCheckResult(path, PathCheckStatus.Available);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new PathCheckResult(path, PathCheckStatus.Missing);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new PathCheckResult(path, PathCheckStatus.AccessDenied, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            return new PathCheckResult(path, PathCheckStatus.Error, exception.Message);
        }
    }
}
