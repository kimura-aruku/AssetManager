using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AssetManager.Application.Startup;

namespace AssetManager.Infrastructure.Logging;

public sealed partial class RollingFileLogger : IApplicationLogger, IDisposable
{
    public const long DefaultMaximumFileSizeBytes = 10 * 1024 * 1024;
    public const int DefaultRetentionDays = 7;

    private readonly string _logsDirectory;
    private readonly TimeProvider _timeProvider;
    private readonly long _maximumFileSizeBytes;
    private readonly int _retentionDays;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RollingFileLogger(
        string logsDirectory,
        TimeProvider? timeProvider = null,
        long maximumFileSizeBytes = DefaultMaximumFileSizeBytes,
        int retentionDays = DefaultRetentionDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFileSizeBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, 1);

        _logsDirectory = Path.GetFullPath(logsDirectory);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maximumFileSizeBytes = maximumFileSizeBytes;
        _retentionDays = retentionDays;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _ = Directory.CreateDirectory(_logsDirectory);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RemoveExpiredLogs();
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task LogInformationAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync("INFO", message, null, cancellationToken);
    }

    public Task LogErrorAsync(
        string context,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return WriteAsync("ERROR", context, exception, cancellationToken);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task WriteAsync(
        string level,
        string message,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = Directory.CreateDirectory(_logsDirectory);
            var timestamp = _timeProvider.GetUtcNow();
            var path = ResolveCurrentLogPath(timestamp);
            var line = new StringBuilder()
                .Append(timestamp.ToString("O", CultureInfo.InvariantCulture))
                .Append(" [")
                .Append(level)
                .Append("] ")
                .Append(message);
            if (exception is not null)
            {
                _ = line.AppendLine().Append(exception);
            }

            _ = line.AppendLine();
            await File.AppendAllTextAsync(
                path,
                line.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ResolveCurrentLogPath(DateTimeOffset timestamp)
    {
        var date = timestamp.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var basePath = Path.Combine(_logsDirectory, $"AssetManager-{date}.log");
        if (!File.Exists(basePath) || new FileInfo(basePath).Length < _maximumFileSizeBytes)
        {
            return basePath;
        }

        for (var sequence = 1; ; sequence++)
        {
            var path = Path.Combine(_logsDirectory, $"AssetManager-{date}.{sequence}.log");
            if (!File.Exists(path) || new FileInfo(path).Length < _maximumFileSizeBytes)
            {
                return path;
            }
        }
    }

    private void RemoveExpiredLogs()
    {
        var cutoffDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(-_retentionDays);
        foreach (var path in Directory.EnumerateFiles(_logsDirectory, "AssetManager-*.log"))
        {
            var match = LogFilePattern().Match(Path.GetFileName(path));
            if (!match.Success
                || !DateOnly.TryParseExact(
                    match.Groups[1].Value,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var logDate))
            {
                continue;
            }

            if (logDate <= cutoffDate)
            {
                File.Delete(path);
            }
        }
    }

    [GeneratedRegex("^AssetManager-(\\d{8})(?:\\.\\d+)?\\.log$")]
    private static partial Regex LogFilePattern();
}
