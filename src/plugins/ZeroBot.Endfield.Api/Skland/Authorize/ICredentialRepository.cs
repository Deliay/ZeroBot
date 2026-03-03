namespace ZeroBot.Endfield.Api.Skland.Authorize;

public interface ICredentialRepository
{
    ValueTask SaveScanIdAsync(string user, string scanId, CancellationToken cancellationToken = default);
    ValueTask<string?> GetUserScanIdAsync(string user, CancellationToken cancellationToken = default);
    ValueTask RemoveUserScanIdAsync(string user, CancellationToken cancellationToken = default);
    
    ValueTask SaveOAuthTokenAsync(string user, string oauthToken, CancellationToken cancellationToken = default);
    ValueTask<HashSet<string>> GetOAuthTokenAsync(string user, CancellationToken cancellationToken = default);
    
    ValueTask SaveCredentialAsync(string user, UserCredential userCredential, CancellationToken cancellationToken = default);
    ValueTask<HashSet<UserCredential>> GetCredentialAsync(string user, CancellationToken cancellationToken = default);
}
