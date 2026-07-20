using AssetManager.Domain.Values;

namespace AssetManager.Application.Paths;

public enum PathEntryKind
{
    File,
    Folder,
}

public enum PathCheckStatus
{
    Unchecked,
    Checking,
    Available,
    Missing,
    AccessDenied,
    Error,
}

public sealed record PathCheckResult(
    string Path,
    PathCheckStatus Status,
    string? Error = null);

public sealed record PathCheckProgress(
    int CheckedCount,
    int TotalCount,
    int UncheckedCount,
    bool IsCanceled,
    PathCheckResult? LatestResult);

public sealed record PathCheckBatchResult(
    IReadOnlyDictionary<string, PathCheckResult> Results,
    int TotalCount,
    int CheckedCount,
    bool IsCanceled);

public sealed record AuxiliaryPathRegistration(
    string Path,
    PathEntryKind ExpectedKind,
    PathCheckResult CheckResult);

public sealed class WindowsPathValidationException(string message) : ArgumentException(message);

public sealed class DuplicateTargetPathException(
    string path,
    string conflictingRecordId,
    string? conflictingRecordName)
    : InvalidOperationException(
        $"対象パス'{path}'はレコード'{conflictingRecordName ?? conflictingRecordId}'で使用されています。")
{
    public string Path { get; } = path;

    public string ConflictingRecordId { get; } = conflictingRecordId;

    public string? ConflictingRecordName { get; } = conflictingRecordName;
}

public interface IWindowsPathFileSystem
{
    DriveType GetDriveType(string driveRoot);

    PathEntryKind? GetExistingKind(string path);

    PathCheckResult Check(string path);
}

public interface IWindowsPathPicker
{
    string? PickFileOrFolder(string title);

    string? PickFile(string title);

    string? PickFolder(string title);
}

public interface IWindowsShellService
{
    void Open(string path);

    void OpenWebUrl(string url);

    void ShowInExplorer(string path, PathEntryKind kind);
}

public static class TargetPathMapping
{
    public static TargetPathKind ToTargetPathKind(this PathEntryKind kind)
    {
        return kind == PathEntryKind.File ? TargetPathKind.File : TargetPathKind.Folder;
    }
}
