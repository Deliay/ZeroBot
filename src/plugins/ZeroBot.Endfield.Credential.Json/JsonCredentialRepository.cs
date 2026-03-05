using System.Text.Json;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Credential.Json;

public record ScanQrCode(string scanId, DateTimeOffset expiredAt);

public record Repository(
    Dictionary<string, ScanQrCode> userScanId,
    Dictionary<string, HashSet<string>> userOAuthTokens,
    Dictionary<string, HashSet<UserCredential>> userCredentials)
{
    public static Repository Empty => new([], [], []);
}


public class JsonCredentialRepository(string path) : ICredentialRepository
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1);

    private async ValueTask<Repository> ReadRepository(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return Repository.Empty;
        
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Repository>(stream, cancellationToken: cancellationToken)
            ?? Repository.Empty;
    }

    private async ValueTask WriteRepository(Repository repository, CancellationToken cancellationToken = default)
    {
        var tempFile = $"{path}.1";
        var exists = File.Exists(path);
        await using var stream = exists ? File.OpenWrite(tempFile) : File.Create(path);
        await JsonSerializer.SerializeAsync(stream, repository, cancellationToken: cancellationToken);
        if (exists) File.Replace(tempFile, path, $"{path}.bak");
    }
    
    private async ValueTask BeginManipulation(Func<Repository, ValueTask<Repository>> operation,
        CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            var repository = await ReadRepository(cancellationToken);
            var nextRepository = await operation(repository);
            await WriteRepository(nextRepository, cancellationToken);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
        
    }
    
    public ValueTask SaveScanIdAsync(string user, string scanId, CancellationToken cancellationToken = default)
    {
        return BeginManipulation((repo) =>
        {
            repo.userScanId.Remove(user);
            repo.userScanId.Add(user, new ScanQrCode(scanId, DateTimeOffset.Now.AddMinutes(15)));
            return ValueTask.FromResult(repo);
        }, cancellationToken);
    }

    public async ValueTask<string?> GetUserScanIdAsync(string user, CancellationToken cancellationToken = default)
    {
        var scanQrCode = (await ReadRepository(cancellationToken)).userScanId.GetValueOrDefault(user);
        return scanQrCode?.expiredAt > DateTimeOffset.Now ? scanQrCode.scanId : null;
    }

    public ValueTask RemoveUserScanIdAsync(string user, CancellationToken cancellationToken = default)
    {
        return BeginManipulation((repo) =>
        {
            repo.userScanId.Remove(user);
            return ValueTask.FromResult(repo);
        }, cancellationToken);
    }

    public ValueTask SaveOAuthTokenAsync(string user, string oauthToken, CancellationToken cancellationToken = default)
    {
        return BeginManipulation((repo) =>
        {
            if (repo.userOAuthTokens.TryGetValue(user, out var userOAuthTokens))
            {
                userOAuthTokens.Add(oauthToken);
            }
            else
            {
                repo.userOAuthTokens.Add(user, [oauthToken]);
            }
            return ValueTask.FromResult(repo);
        }, cancellationToken);
    }

    public async ValueTask<HashSet<string>> GetOAuthTokenAsync(string user, CancellationToken cancellationToken = default)
    {
        return (await ReadRepository(cancellationToken)).userOAuthTokens.GetValueOrDefault(user) ?? [];
    }

    public ValueTask SaveCredentialAsync(string user, UserCredential userCredential, CancellationToken cancellationToken = default)
    {
        return BeginManipulation((repo) =>
        {
            if (repo.userCredentials.TryGetValue(user, out var userCredentials))
            {
                userCredentials.Add(userCredential);
            }
            else
            {
                repo.userCredentials.Add(user, [userCredential]);
            }

            return ValueTask.FromResult(repo);
        }, cancellationToken);
    }

    public async ValueTask<HashSet<UserCredential>> GetCredentialAsync(string user, CancellationToken cancellationToken = default)
    {
        return (await ReadRepository(cancellationToken)).userCredentials.GetValueOrDefault(user) ?? [];
    }
}