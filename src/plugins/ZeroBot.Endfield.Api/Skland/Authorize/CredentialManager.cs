using Polly;
using Polly.Retry;

namespace ZeroBot.Endfield.Api.Skland.Authorize;

public class CredentialManager(HypergryphClient client, ICredentialRepository repository)
{
    public async ValueTask<string> GenerateLoginQrCodePayload(string user, CancellationToken cancellationToken)
    {
        var (scanId, scanUrl) = await client.GenerateLoginQrCode(cancellationToken);

        await repository.SaveScanIdAsync(user, scanId, cancellationToken);
        return scanUrl;
    }
    
    
    private static readonly ResiliencePipeline<Response<LoginScanStatusResponse>> WaitScanPipeline
        = new ResiliencePipelineBuilder<Response<LoginScanStatusResponse>>()
        .AddRetry(new()
        {
            Delay = TimeSpan.FromSeconds(5),
            MaxRetryAttempts = 60,
            ShouldHandle = new PredicateBuilder<Response<LoginScanStatusResponse>>()
                .HandleResult((response) => response.status is 100 or 101),
        }).Build();

    public async ValueTask<UserCredential> WaitScanAsync(string user, CancellationToken cancellationToken)
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

        var authorization = await client.GrantAuthorizationCodeAsync(oAuthToken, cancellationToken);
        var credential = await client.GenerateZonCredentialAsync(authorization, cancellationToken);

        await repository.SaveCredentialAsync(user, credential, cancellationToken);

        return credential;
    }

    public ValueTask<HashSet<UserCredential>> GetCurrentCredentialAsync(string user, CancellationToken cancellationToken)
    {
        return repository.GetCredentialAsync(user, cancellationToken);
    }

    public async ValueTask RenewalRefreshTokenAsync(string user, CancellationToken cancellationToken = default)
    {
        var credentials = await repository.GetCredentialAsync(user, cancellationToken);
        foreach (var userCredential in credentials)
        {
            var result = await client.GenerateZonRefreshTokenAsync(userCredential, cancellationToken);
            userCredential.RefreshToken = result.token;
            userCredential.TokenExpiredAt = DateTimeOffset.Now + TimeSpan.FromHours(1);
            await repository.SaveCredentialAsync(user, userCredential, cancellationToken);
        }
    }
}