namespace AssetManager.Infrastructure.Persistence;

public class DataPersistenceException : Exception
{
    public DataPersistenceException(string message)
        : base(message)
    {
    }

    public DataPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class CriticalDataFileException : DataPersistenceException
{
    public CriticalDataFileException(string path, Exception innerException)
        : base($"必須データファイルを読み込めませんでした: {path}", innerException)
    {
        Path = path;
    }

    public string Path { get; }
}

public sealed class UnsupportedSchemaVersionException : DataPersistenceException
{
    public UnsupportedSchemaVersionException(int actualVersion, int supportedVersion)
        : base($"schemaVersion {actualVersion} は未対応です。対応上限は {supportedVersion} です。")
    {
        ActualVersion = actualVersion;
        SupportedVersion = supportedVersion;
    }

    public int ActualVersion { get; }

    public int SupportedVersion { get; }
}

public sealed class SchemaMigrationRequiredException : DataPersistenceException
{
    public SchemaMigrationRequiredException(int actualVersion, int currentVersion)
        : base($"schemaVersion {actualVersion} から {currentVersion} への移行が必要です。")
    {
        ActualVersion = actualVersion;
        CurrentVersion = currentVersion;
    }

    public int ActualVersion { get; }

    public int CurrentVersion { get; }
}

public static class SchemaVersionGuard
{
    public static void EnsureCurrent(int actualVersion)
    {
        if (actualVersion > Json.JsonDefaults.CurrentSchemaVersion)
        {
            throw new UnsupportedSchemaVersionException(
                actualVersion,
                Json.JsonDefaults.CurrentSchemaVersion);
        }

        if (actualVersion < Json.JsonDefaults.CurrentSchemaVersion)
        {
            throw new SchemaMigrationRequiredException(
                actualVersion,
                Json.JsonDefaults.CurrentSchemaVersion);
        }
    }
}
