using Polly;
using Polly.Retry;

namespace ZeroBot.Endfield.Api.Skland.Authorize;

public class CredentialManager(HypergryphClient client, ICredentialRepository repository)
{
    public async ValueTask<LoginQrCodeResponse> GenerateLoginQrCodePayload(string user, CancellationToken cancellationToken = default)
    {
        var response = await client.GenerateLoginQrCode(cancellationToken);

        await repository.SaveScanIdAsync(user, response.scanId, cancellationToken);
        return response;
    }

    public ValueTask<string?> GetUserScanIdAsync(string user, CancellationToken cancellationToken = default)
    {
        return repository.GetUserScanIdAsync(user, cancellationToken);
    }
    
    public ValueTask FlushUserScanIdAsync(CancellationToken cancellationToken = default)
        => repository.FlushUserScanIdAsync(cancellationToken);
    
    private static readonly ResiliencePipeline<Response<LoginScanStatusResponse>> WaitScanPipeline
        = new ResiliencePipelineBuilder<Response<LoginScanStatusResponse>>()
        .AddRetry(new RetryStrategyOptions<Response<LoginScanStatusResponse>>
        {
            Delay = TimeSpan.FromSeconds(5),
            MaxRetryAttempts = 60,
            ShouldHandle = new PredicateBuilder<Response<LoginScanStatusResponse>>()
                .HandleResult((response) => response.status is 100 or 101),
        }).Build();

    public async ValueTask<UserCredential> WaitScanAsync(string user, CancellationToken cancellationToken = default)
    {
        var scanId = await repository.GetUserScanIdAsync(user, cancellationToken);
        if (scanId is null)
            throw new InvalidOperationException("Current user does not generate any QrCode waiting for scan");
        
        var scanResult = await WaitScanPipeline.ExecuteAsync(async (token) =>
        {
            var scanStatus = await client.GetLoginQrCodeStatus(scanId, token);
            return scanStatus;
        }, cancellationToken);
        scanResult.EnsureSuccessStatusCode();
        
        var (oAuthToken, _ ) = await client.GetOAuthTokenByScanCode(scanResult.data.scanCode, cancellationToken);
        await repository.SaveOAuthTokenAsync(user, oAuthToken, cancellationToken);

        var credential = await client.GenerateZonCredentialAsync(oAuthToken, cancellationToken);

        await repository.SaveCredentialAsync(user, credential, cancellationToken);
        await repository.RemoveUserScanIdAsync(user, cancellationToken);
        return credential;
    }

    public ValueTask<HashSet<UserCredential>> GetCurrentCredentialAsync(string user, CancellationToken cancellationToken = default)
    {
        return repository.GetCredentialAsync(user, cancellationToken);
    }

    public ValueTask<UserCredential?> GetCredentialAsync(string user, string credentialId,
        CancellationToken cancellationToken = default)
    {
        return repository.GetCredentialAsync(user, credentialId, cancellationToken);
    }

    public ValueTask RemoveCredentialAsync(string user, string id, CancellationToken cancellationToken = default)
    {
        return repository.RemoveCredentialAsync(user, id, cancellationToken);
    }

    public ValueTask RenewalSingleRefreshTokenAsync(string user, string credentialId,
        CancellationToken cancellationToken = default)
    {
        return RenewalRefreshTokenCoreAsync(
            user, 
            (credential) => credential.Id == credentialId,
            false,
            cancellationToken);
    }

    public ValueTask RenewalRefreshTokenAsync(string user, CancellationToken cancellationToken = default)
    {
        return RenewalRefreshTokenCoreAsync(user, (_) => true, true, cancellationToken);
    }

    private async ValueTask RenewalRefreshTokenCoreAsync(string user,
        Func<UserCredential, bool> predictor,
        bool refreshNoCredentialOAuthToken = true,
        CancellationToken cancellationToken = default)
    {
        var credentials = await repository.GetCredentialAsync(user, cancellationToken);
        var expiredCredentials =
            credentials
                .Where(userCredential => userCredential.TokenExpiredAt <= DateTimeOffset.Now)
                .Where(predictor);

        foreach (var userCredential in expiredCredentials)
        {
            var newCredential = await client.GenerateZonCredentialAsync(userCredential.OAuthToken, cancellationToken);
            await repository.SaveCredentialAsync(user, newCredential, cancellationToken);
        }

        if (!refreshNoCredentialOAuthToken) return;

        var allCredentials = (await repository.GetCredentialAsync(user, cancellationToken))
            .ToDictionary((c) => c.OAuthToken, c => c);

        var allOAuthTokens = (await repository.GetOAuthTokenAsync(user, cancellationToken))
            .Where(token => !allCredentials.ContainsKey(token));

        foreach (var oAuthToken in allOAuthTokens)
        {
            var newCredential = await client.GenerateZonCredentialAsync(oAuthToken, cancellationToken);
            await repository.SaveCredentialAsync(user, newCredential, cancellationToken);
        }
    }
}
