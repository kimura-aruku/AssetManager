using AssetManager.Application.Paths;
using AssetManager.Application.Data;
using AssetManager.Application.Status;
using AssetManager.Domain.Fields;
using AssetManager.Domain.Licensing;
using AssetManager.Domain.Records;
using AssetManager.Domain.Values;
using AssetManager.UnitTests.Testing;

namespace AssetManager.UnitTests.Paths;

public sealed class PathCheckServiceTests
{
    [Fact]
    public async Task CheckAllUsesAtMostEightWorkersAndCachesEquivalentPaths()
    {
        var fileSystem = new TrackingFileSystem(TimeSpan.FromMilliseconds(10));
        var service = new PathCheckService(fileSystem);
        var paths = Enumerable.Range(0, 24).Select(index => $@"C:\Assets\{index}.png").ToArray();

        var first = await service.CheckAllAsync(paths, refreshCachedResults: false);
        var second = await service.CheckAllAsync(
            paths.Concat([@"c:/assets/0.PNG"]),
            refreshCachedResults: false);

        Assert.Equal(24, first.CheckedCount);
        Assert.Equal(24, second.CheckedCount);
        Assert.InRange(fileSystem.MaximumActiveChecks, 2, PathCheckService.MaximumConcurrency);
        Assert.Equal(24, fileSystem.CheckCount);
    }

    [Fact]
    public async Task CancelStopsNewChecksAndKeepsCompletedResults()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new TrackingFileSystem(
            TimeSpan.FromMilliseconds(30),
            completedChecks =>
            {
                if (completedChecks == 1)
                {
                    cancellation.Cancel();
                }
            });
        var service = new PathCheckService(fileSystem);
        var paths = Enumerable.Range(0, 40).Select(index => $@"C:\Assets\{index}.png");

        var result = await service.CheckAllAsync(
            paths,
            refreshCachedResults: true,
            cancellationToken: cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.InRange(result.CheckedCount, 1, 39);
        Assert.Equal(result.CheckedCount, result.Results.Count);
        Assert.Equal(result.CheckedCount, service.Cache.Count);
    }

    [Fact]
    public async Task ChangedPathAlwaysRefreshesCachedResult()
    {
        var fileSystem = new TrackingFileSystem(TimeSpan.Zero);
        var service = new PathCheckService(fileSystem);

        _ = await service.CheckChangedPathAsync(@"C:\Assets\file.png");
        _ = await service.CheckChangedPathAsync(@"c:/assets/FILE.PNG");

        Assert.Equal(2, fileSystem.CheckCount);
        Assert.Single(service.Cache);
    }

    [Fact]
    public async Task BatchKeepsMissingAccessDeniedAndErrorStatusesDistinct()
    {
        var service = new PathCheckService(new StatusFileSystem());

        var result = await service.CheckAllAsync(
            [@"C:\missing", @"C:\denied", @"C:\error"],
            refreshCachedResults: true);

        Assert.Equal(PathCheckStatus.Missing, result.Results[@"C:\missing"].Status);
        Assert.Equal(PathCheckStatus.AccessDenied, result.Results[@"C:\denied"].Status);
        Assert.Equal(PathCheckStatus.Error, result.Results[@"C:\error"].Status);
    }

    [Fact]
    public async Task SavedRecordPathCheckPopulatesCacheBeforeRowEvaluation()
    {
        var now = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var targetDefinition = BuiltInFieldCatalog.All.Single(
            definition => definition.Id == BuiltInFieldIds.TargetPath);
        var receiptDefinition = BuiltInFieldCatalog.All.Single(
            definition => definition.Id == BuiltInFieldIds.ReceiptPath);
        var record = AssetRecord.Create(now)
            .SetValue(
                targetDefinition,
                new TargetPathFieldValue(TargetPathKind.File, @"C:\Assets\item.png"),
                now)
            .SetValue(
                receiptDefinition,
                new FilePathFieldValue(@"C:\Assets\receipt.pdf"),
                now);
        var store = new TestDataStore(new AssetManagerDataSnapshot(
            BuiltInFieldCatalog.All,
            [],
            [],
            [],
            [record]));
        var coordinator = new RecordPathCheckCoordinator(
            store,
            new PathCheckService(new AvailableFileSystem()));

        var result = await coordinator.CheckRecordAsync(record);
        var indicators = RecordIndicatorEvaluator.Evaluate(
            record,
            new AssetDate(new DateOnly(2026, 7, 20)),
            new LicenseWarningPolicy(),
            coordinator.Cache);

        Assert.Equal(2, result.CheckedCount);
        Assert.Equal(2, coordinator.Cache.Count);
        Assert.All(coordinator.Cache.Values, item => Assert.Equal(PathCheckStatus.Available, item.Status));
        Assert.Equal(RecordIndicatorKind.PathsAvailable, Assert.Single(indicators).Kind);
    }

    private sealed class TrackingFileSystem(
        TimeSpan delay,
        Action<int>? onCheckCompleted = null) : IWindowsPathFileSystem
    {
        private int _activeChecks;
        private int _checkCount;
        private int _completedChecks;
        private int _maximumActiveChecks;

        public int CheckCount => _checkCount;

        public int MaximumActiveChecks => _maximumActiveChecks;

        public DriveType GetDriveType(string driveRoot) => DriveType.Fixed;

        public PathEntryKind? GetExistingKind(string path) => PathEntryKind.File;

        public PathCheckResult Check(string path)
        {
            _ = Interlocked.Increment(ref _checkCount);
            var active = Interlocked.Increment(ref _activeChecks);
            UpdateMaximum(active);
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    Thread.Sleep(delay);
                }

                var completedChecks = Interlocked.Increment(ref _completedChecks);
                onCheckCompleted?.Invoke(completedChecks);
                return new PathCheckResult(path, PathCheckStatus.Available);
            }
            finally
            {
                _ = Interlocked.Decrement(ref _activeChecks);
            }
        }

        private void UpdateMaximum(int value)
        {
            while (true)
            {
                var current = _maximumActiveChecks;
                if (value <= current
                    || Interlocked.CompareExchange(ref _maximumActiveChecks, value, current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class StatusFileSystem : IWindowsPathFileSystem
    {
        public DriveType GetDriveType(string driveRoot) => DriveType.Fixed;

        public PathEntryKind? GetExistingKind(string path) => null;

        public PathCheckResult Check(string path)
        {
            var status = path.EndsWith("missing", StringComparison.Ordinal)
                ? PathCheckStatus.Missing
                : path.EndsWith("denied", StringComparison.Ordinal)
                    ? PathCheckStatus.AccessDenied
                    : PathCheckStatus.Error;
            return new PathCheckResult(path, status);
        }
    }

    private sealed class AvailableFileSystem : IWindowsPathFileSystem
    {
        public DriveType GetDriveType(string driveRoot) => DriveType.Fixed;

        public PathEntryKind? GetExistingKind(string path) => PathEntryKind.File;

        public PathCheckResult Check(string path) => new(path, PathCheckStatus.Available);
    }
}
