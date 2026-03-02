using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZeroBot.Endfield.Api.Skland.Sign;

public class DeviceIdManager
{
    private static readonly HttpClient HttpClient = new();
    private static async Task<string> RequestDeviceId(string encrypted, string epBase64, CancellationToken cancellationToken = default)
    {
        // Request device ID
        var res = await HttpClient.PostAsJsonAsync("https://fp-it.portal101.cn/deviceprofile/v4",
            new {
                appId = "default",
                compress = 2,
                data = encrypted,
                encode = 5,
                ep = epBase64,
                organization = "UWXspnCCJN4sfYlNfqps",
                os = "web",
            }, cancellationToken);
        res.EnsureSuccessStatusCode();
        var response = await res.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);

        if (response!.RootElement.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() != 1100)
        {
            throw new Exception($"Device ID generation failed: {response.RootElement.ToString()}");
        }

        return $"B{response.RootElement.GetProperty("detail").GetProperty("deviceId").GetString()}";
    }
    public async Task<string> GenerateDeviceId(CancellationToken cancellationToken = default)
    {
        // Generate UUID and priId
        var uid = Guid.NewGuid().ToString();
        using var priIdMd5 = MD5.Create();
        var priIdHash = priIdMd5.ComputeHash(Encoding.UTF8.GetBytes(uid)).Take(8).ToArray();
        var priIdHex = BitConverter.ToString(priIdHash).Replace("-", "").ToLower();

        // RSA encrypt UUID
        var publicKeyDer = Convert.FromBase64String(SklandConstants.RsaPublicKey);
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(publicKeyDer, out _);
        var encryptedUid = rsa.Encrypt(Encoding.UTF8.GetBytes(uid), RSAEncryptionPadding.Pkcs1);
        var epBase64 = Convert.ToBase64String(encryptedUid);

        // Build browser fingerprint
        var inMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var browser = new Dictionary<string, object>(SklandConstants.BrowserEnv)
        {
            ["vpw"] = Guid.NewGuid().ToString(),
            ["trees"] = Guid.NewGuid().ToString(),
            ["svm"] = inMs,
            ["pmf"] = inMs
        };

        // Build target data
        var desTarget = new Dictionary<string, object>(SklandConstants.DesTarget)
        {
            ["smid"] = SklandEncryption.GetSmid()
        };
        foreach (var entry in browser)
        {
            desTarget[entry.Key] = entry.Value;
        }

        // Generate tn
        var tnInput = SklandEncryption.GetTn(desTarget);
        using var md5 = MD5.Create();
        desTarget["tn"] = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(tnInput))).Replace("-", "").ToLower();

        // Apply DES rules and compress
        var desResult = SklandEncryption.ApplyDesRules(desTarget);
        var jsonStr = JsonSerializer.Serialize(desResult, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        using var ms = new MemoryStream();
        await using var gzip = new GZipStream(ms, CompressionLevel.SmallestSize);
        await gzip.WriteAsync(Encoding.UTF8.GetBytes(jsonStr), 0, Encoding.UTF8.GetBytes(jsonStr).Length, cancellationToken);
        var compressed = ms.ToArray();

        // AES encrypt
        var encrypted = SklandEncryption.AesEncrypt(compressed, Encoding.UTF8.GetBytes(priIdHex));

        return await RequestDeviceId(encrypted, epBase64, cancellationToken);
    }
}