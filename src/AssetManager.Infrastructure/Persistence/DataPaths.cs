namespace AssetManager.Infrastructure.Persistence;

public sealed class AppDataPaths
{
    public const string PublisherDirectoryName = "kimura-aruku";
    public const string ApplicationDirectoryName = "AssetManager";

    public AppDataPaths(string localApplicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataRoot);
        var root = Path.GetFullPath(localApplicationDataRoot);

        FixedAppRoot = Path.Combine(root, PublisherDirectoryName, ApplicationDirectoryName);
        BootstrapFile = Path.Combine(FixedAppRoot, "bootstrap.json");
        LogsDirectory = Path.Combine(FixedAppRoot, "logs");
        DefaultDataRoot = Path.Combine(FixedAppRoot, "Data");
    }

    public string FixedAppRoot { get; }

    public string BootstrapFile { get; }

    public string LogsDirectory { get; }

    public string DefaultDataRoot { get; }

    public static AppDataPaths CreateDefault()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        return new AppDataPaths(localApplicationData);
    }

    public void EnsureFixedDirectories()
    {
        _ = Directory.CreateDirectory(FixedAppRoot);
        _ = Directory.CreateDirectory(LogsDirectory);
    }
}

public sealed class DataRootLayout
{
    public DataRootLayout(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
        ManifestFile = Path.Combine(RootDirectory, "manifest.json");
        RecordsDirectory = Path.Combine(RootDirectory, "records");
        DefinitionsDirectory = Path.Combine(RootDirectory, "definitions");
        FieldsFile = Path.Combine(DefinitionsDirectory, "fields.json");
        AssetTypesFile = Path.Combine(DefinitionsDirectory, "asset-types.json");
        TagsFile = Path.Combine(DefinitionsDirectory, "tags.json");
        SettingsFile = Path.Combine(RootDirectory, "settings.json");
        ViewsFile = Path.Combine(RootDirectory, "views.json");
        UndoDirectory = Path.Combine(RootDirectory, "undo");
        TransactionsDirectory = Path.Combine(RootDirectory, "transactions");
    }

    public string RootDirectory { get; }

    public string ManifestFile { get; }

    public string RecordsDirectory { get; }

    public string DefinitionsDirectory { get; }

    public string FieldsFile { get; }

    public string AssetTypesFile { get; }

    public string TagsFile { get; }

    public string SettingsFile { get; }

    public string ViewsFile { get; }

    public string UndoDirectory { get; }

    public string TransactionsDirectory { get; }

    public void EnsureDirectories()
    {
        _ = Directory.CreateDirectory(RootDirectory);
        _ = Directory.CreateDirectory(RecordsDirectory);
        _ = Directory.CreateDirectory(DefinitionsDirectory);
        _ = Directory.CreateDirectory(UndoDirectory);
        _ = Directory.CreateDirectory(TransactionsDirectory);
    }

    public string ResolveRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var resolved = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        var relative = Path.GetRelativePath(RootDirectory, resolved);

        if (relative == ".."
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new ArgumentException("データルート外のパスは指定できません。", nameof(relativePath));
        }

        return resolved;
    }
}
