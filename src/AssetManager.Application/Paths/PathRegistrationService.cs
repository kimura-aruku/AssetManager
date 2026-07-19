using AssetManager.Domain.Values;

namespace AssetManager.Application.Paths;

public sealed class PathRegistrationService
{
    private readonly IWindowsPathFileSystem _fileSystem;
    private readonly IWindowsPathPicker? _picker;

    public PathRegistrationService(
        IWindowsPathFileSystem fileSystem,
        IWindowsPathPicker? picker = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _picker = picker;
    }

    public TargetPathFieldValue RegisterTarget(string input)
    {
        var path = ValidateLocalFixedPath(input);
        var kind = _fileSystem.GetExistingKind(path)
            ?? throw new WindowsPathValidationException("存在するファイルまたはフォルダーを指定してください。");
        return new TargetPathFieldValue(kind.ToTargetPathKind(), path);
    }

    public TargetPathFieldValue RegisterDroppedTarget(IEnumerable<string> droppedPaths)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);
        var paths = droppedPaths.ToArray();
        if (paths.Length != 1)
        {
            throw new WindowsPathValidationException("対象パスには1件だけドロップしてください。");
        }

        return RegisterTarget(paths[0]);
    }

    public AuxiliaryPathRegistration RegisterAuxiliary(
        string input,
        PathEntryKind expectedKind)
    {
        var path = ValidateLocalFixedPath(input);
        return new AuxiliaryPathRegistration(
            path,
            expectedKind,
            _fileSystem.Check(path));
    }

    public IReadOnlyList<AuxiliaryPathRegistration> RegisterDroppedAuxiliary(
        IEnumerable<string> droppedPaths)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);
        return droppedPaths.Select(path =>
        {
            var normalized = ValidateLocalFixedPath(path);
            var kind = _fileSystem.GetExistingKind(normalized) ?? PathEntryKind.File;
            return new AuxiliaryPathRegistration(normalized, kind, _fileSystem.Check(normalized));
        }).ToArray();
    }

    public TargetPathFieldValue? PickTargetFile(string title = "対象ファイルを選択")
    {
        EnsurePicker();
        var path = _picker!.PickFile(title);
        return path is null ? null : RegisterTarget(path);
    }

    public TargetPathFieldValue? PickTargetFolder(string title = "対象フォルダーを選択")
    {
        EnsurePicker();
        var path = _picker!.PickFolder(title);
        return path is null ? null : RegisterTarget(path);
    }

    public AuxiliaryPathRegistration? PickAuxiliaryFile(string title)
    {
        EnsurePicker();
        var path = _picker!.PickFile(title);
        return path is null ? null : RegisterAuxiliary(path, PathEntryKind.File);
    }

    public AuxiliaryPathRegistration? PickAuxiliaryFolder(string title)
    {
        EnsurePicker();
        var path = _picker!.PickFolder(title);
        return path is null ? null : RegisterAuxiliary(path, PathEntryKind.Folder);
    }

    private string ValidateLocalFixedPath(string input)
    {
        var path = WindowsPathNormalizer.NormalizeForStorage(input);
        var root = Path.GetPathRoot(path)
            ?? throw new WindowsPathValidationException("ドライブルートを判定できません。");
        var driveType = _fileSystem.GetDriveType(root);
        if (driveType != DriveType.Fixed)
        {
            throw new WindowsPathValidationException(
                $"固定ローカルドライブ以外は登録できません（{driveType}）。");
        }

        return path;
    }

    private void EnsurePicker()
    {
        if (_picker is null)
        {
            throw new InvalidOperationException("パス選択ダイアログが構成されていません。");
        }
    }
}
