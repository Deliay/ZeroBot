using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Polly;
using Polly.Retry;
using ZeroBot.Endfield.Api.Skland.Authorize;

namespace ZeroBot.Endfield.Api.Skland;

public class HypergryphClient : HttpClient
{
    public const string UserAgent = "Mozilla/5.0 (Linux; Android 12; SM-A5560 Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36; SKLand/1.52.1";

    public static readonly IReadOnlyDictionary<string, string> BaseHeaders = new Dictionary<string, string>
    {
        {"User-Agent", UserAgent},
        {"Accept-Encoding", "gzip"},
        {"Connection", "close"},
        {"X-Requested-With", "com.hypergryph.skland"},
    };
    
    public static (string sign, Dictionary<string, string> headerCa) GenerateSignature(string token, string path, string bodyOrQuery, string did)
    {
        var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        var headerCa = new Dictionary<string, string>
        {
            {"platform", "3"},
            {"timestamp", timestamp.ToString()},
            {"dId", did},
            {"vName", "1.0.0"},
        };
        var headerCaStr = JsonSerializer.Serialize(headerCa, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        var s = $"{path}{bodyOrQuery}{timestamp}{headerCaStr}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(token));
        var hmacResult = hmac.ComputeHash(Encoding.UTF8.GetBytes(s));
        var hmacHex = BitConverter.ToString(hmacResult).Replace("-", "").ToLower();

        using var md5 = MD5.Create();
        var sign = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(hmacHex))).Replace("-", "").ToLower();

        return (sign, headerCa);
    }
    
    public static Dictionary<string, string> GetSignedHeaders(string url, HttpMethod method, string? body, UserCredential cred)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;
        var query = uri.Query.TrimStart('?');
        var did = cred.DeviceId;

        string sign;
        Dictionary<string, string> headerCa;

        if (method == HttpMethod.Get)
        {
            (sign, headerCa) = GenerateSignature(cred.RefreshToken, path, query, did);
        }
        else
        {
            (sign, headerCa) = GenerateSignature(cred.RefreshToken, path, body ?? "", did);
        }

        var headers = new Dictionary<string, string>(BaseHeaders)
        {
            ["dId"] = did,
            ["cred"] = cred.Cred,
            ["sign"] = sign
        };
        foreach (var entry in headerCa)
        {
            headers[entry.Key] = entry.Value;
        }

        return headers;
    }
}