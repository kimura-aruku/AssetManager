using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AssetManager.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Semaphore _semaphore;
    private readonly EventWaitHandle _activationEvent;
    private readonly bool _ownsSemaphore;
    private bool _disposed;

    public SingleInstanceCoordinator(string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        var scope = CreateUserScope(applicationId);
        _activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            $"Local\\{scope}.Activate");
        _semaphore = new Semaphore(
            initialCount: 1,
            maximumCount: 1,
            $"Local\\{scope}.Semaphore");
        _ownsSemaphore = _semaphore.WaitOne(TimeSpan.Zero);
        IsPrimary = _ownsSemaphore;
        if (!IsPrimary)
        {
            _ = _activationEvent.Set();
        }
    }

    public bool IsPrimary { get; }

    public Task ListenForActivationAsync(
        Action activate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activate);
        if (!IsPrimary)
        {
            throw new InvalidOperationException("プライマリインスタンスだけが通知を待機できます。");
        }

        return Task.Run(
            () => ListenForActivation(activate, cancellationToken),
            CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsSemaphore)
        {
            _semaphore.Release();
        }

        _semaphore.Dispose();
        _activationEvent.Dispose();
        _disposed = true;
    }

    private void ListenForActivation(Action activate, CancellationToken cancellationToken)
    {
        var handles = new WaitHandle[]
        {
            _activationEvent,
            cancellationToken.WaitHandle,
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            var signaled = WaitHandle.WaitAny(handles);
            if (signaled == 1)
            {
                return;
            }

            activate();
        }
    }

    private static string CreateUserScope(string applicationId)
    {
        var userIdentity = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userIdentity));
        return $"{applicationId}.{Convert.ToHexString(hash.AsSpan(0, 12))}";
    }
}
