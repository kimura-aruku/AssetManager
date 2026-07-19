using AssetManager.Infrastructure.Windows;

namespace AssetManager.IntegrationTests.Startup;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public async Task SecondaryInstanceSignalsPrimaryInstance()
    {
        var applicationId = $"AssetManager.Tests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(applicationId);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var activated = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = primary.ListenForActivationAsync(
            () => activated.TrySetResult(),
            cancellation.Token);

        using var secondary = new SingleInstanceCoordinator(applicationId);

        Assert.True(primary.IsPrimary);
        Assert.False(secondary.IsPrimary);
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        await listener;
    }
}
