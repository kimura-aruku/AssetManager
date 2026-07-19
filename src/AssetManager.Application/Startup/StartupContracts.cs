namespace AssetManager.Application.Startup;

public sealed record StartupProgress(string Message, int CompletedSteps, int TotalSteps);

public enum StartupFailureKind
{
    DataCreation,
    DataLoading,
}

public sealed class StartupException : Exception
{
    public StartupException(
        StartupFailureKind failureKind,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    public StartupFailureKind FailureKind { get; }
}

public sealed record StartupResult(
    string DataRoot,
    int RecordCount,
    int RepairedValueCount,
    int ExcludedRecordCount,
    bool CreatedInitialData,
    bool CheckPathsOnStartup);

public interface IStartupInitializer
{
    Task<StartupResult> InitializeAsync(
        IProgress<StartupProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IApplicationLogger
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task LogInformationAsync(
        string message,
        CancellationToken cancellationToken = default);

    Task LogErrorAsync(
        string context,
        Exception exception,
        CancellationToken cancellationToken = default);
}
