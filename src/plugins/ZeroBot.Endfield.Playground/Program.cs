using System.Globalization;
using Net.Codecrete.QrCodeGenerator;
using ZeroBot.Endfield.Api.Skland;
using ZeroBot.Endfield.Api.Skland.Authorize;
using ZeroBot.Endfield.Api.Skland.Player;
using ZeroBot.Endfield.Credential.Json;
using ZeroBot.Endfield.Playground;

var credentialRepository = new JsonCredentialRepository("credentials.json");
var hypergryphClient = new HypergryphClient();
var credentialManager = new CredentialManager(hypergryphClient, credentialRepository);
using var cts = new CancellationTokenSource();
var cancellationToken = cts.Token;

const string user = "test";

Console.WriteLine($"Current working directory: {Directory.GetCurrentDirectory()}");

await credentialManager.RenewalRefreshTokenAsync(user, cancellationToken);
var credentials = await credentialManager.GetCurrentCredentialAsync(user, cancellationToken);

if (credentials.Count == 0)
{
    var (_, scanUrl) = await credentialManager.GenerateLoginQrCodePayload(user, cancellationToken);
    Console.WriteLine(QrCode.EnhancedEncodeText(scanUrl));

    var credential = await credentialManager.WaitScanAsync(user, cancellationToken);
}

credentials = await credentialManager.GetCurrentCredentialAsync(user, cancellationToken);

foreach (var credential in credentials)
{
    var bindings = await hypergryphClient.GetPlayerBindings(credential, cancellationToken);
    foreach (var userAppRole in bindings.Flat())
    {
        Console.WriteLine($"{userAppRole.appCode}: {userAppRole.channelName}" +
                          $" - {userAppRole.gameName} - uid:{userAppRole.uid}" +
                          $" - {userAppRole.roleNickname}/{userAppRole.nickName} - roleId:{userAppRole.roleId}");

        try
        {
            await hypergryphClient.DailySignAsync(credential, userAppRole, cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}
