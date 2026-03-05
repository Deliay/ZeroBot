using System.Collections.Concurrent;
using EmberFramework.Abstraction;
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.Logging;
using Milky.Net.Model;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Component;

public class ScanQrCodeTaskManager(CredentialManager credentialManager, ILogger<ScanQrCodeTaskManager> logger) : IComponentInitializer
{
    public void Enqueue(string userId, Func<UserCredential, ValueTask> onComplete, CancellationToken cancellationToken)
    {
        using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, token);
        var taskToken = taskCts.Token;
        Task.Run(async () =>
        {
            try
            {
                var result = await credentialManager.WaitScanAsync(userId, taskToken);
                await onComplete(result);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while waiting for QR code scan");
            }
        }, token);
    }

    private CancellationTokenSource? _cts;
    private CancellationToken token => _cts?.Token ?? CancellationToken.None;
    
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await credentialManager.FlushUserScanIdAsync(cancellationToken);
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Dispose();
    }
}
