using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.Logging;

namespace AssetManager.IntegrationTests.Startup;

public sealed class RollingFileLoggerTests
{
    private static readonly DateTimeOffset TestTime = new(
        2026,
        7,
        19,
        12,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task LoggerUsesDailyFilesAndRotatesAtMaximumSize()
    {
        using var temporary = new TemporaryDirectory();
        using var logger = new RollingFileLogger(
            temporary.Path,
            new FixedTimeProvider(TestTime),
            maximumFileSizeBytes: 1);
        await logger.InitializeAsync();

        await logger.LogInformationAsync("first");
        await logger.LogErrorAsync("second", new InvalidOperationException("detail"));

        Assert.True(File.Exists(temporary.GetPath("AssetManager-20260719.log")));
        var rotated = temporary.GetPath("AssetManager-20260719.1.log");
        Assert.True(File.Exists(rotated));
        var content = await File.ReadAllTextAsync(rotated);
        Assert.Contains("[ERROR] second", content, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException: detail", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitializationDeletesLogsThatReachedSevenDaysOld()
    {
        using var temporary = new TemporaryDirectory();
        var expired = temporary.GetPath("AssetManager-20260712.log");
        var retained = temporary.GetPath("AssetManager-20260713.log");
        await File.WriteAllTextAsync(expired, "expired");
        await File.WriteAllTextAsync(retained, "retained");
        using var logger = new RollingFileLogger(
            temporary.Path,
            new FixedTimeProvider(TestTime));

        await logger.InitializeAsync();

        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(retained));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
