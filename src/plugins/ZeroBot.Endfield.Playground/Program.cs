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

var user = "644676751";

Console.WriteLine($"Current working directory: {Directory.GetCurrentDirectory()}");

var credentials = await credentialManager.GetCurrentCredentialAsync(user, cancellationToken);

if (credentials.Count == 0)
{
    var scanUrl = await credentialManager.GenerateLoginQrCodePayload(user, cancellationToken);
    Console.WriteLine(QrCode.EnhancedEncodeText(scanUrl));

    var credential = await credentialManager.WaitScanAsync(user, cancellationToken);

    credentials = await credentialManager.GetCurrentCredentialAsync(user, cancellationToken);
}
else
{
    await credentialManager.RenewalRefreshTokenAsync(user, cancellationToken);
}

foreach (var credential in credentials)
{
    var bindings = await hypergryphClient.GetPlayerBindings(credential, cancellationToken);

    foreach (var binding in bindings.list)
    {
        foreach (var userAppBinding in binding.bindingList)
        {
            foreach (var userRole in userAppBinding.roles)
            {
                Console.WriteLine($"{binding.appCode}: {userAppBinding.channelName}" +
                                  $" - {userAppBinding.gameName} - uid:{userAppBinding.uid}" +
                                  $" - {userRole.nickname}/{userAppBinding.nickName} - roleId:{userRole.roleId}");
            }
        }
    }
}
