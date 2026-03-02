using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZeroBot.Endfield.Api.Skland.Authorize;


public static class DeviceIdGenerator
{
    private const string RsaPublicKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCmxMNr7n8ZeT0tE1R9j/mPixoinPkeM+k4VGIn/s0k7N5rJAfnZ0eMER+QhwFvshzo0LNmeUkpR8uIlU/GEVr8mN28sKmwd2gpygqj0ePnBmOW4v0ZVwbSYK+izkhVFk2V/doLoMbWy6b+UnA8mkjvg0iYWRByfRsK2gdl7llqCwIDAQAB";

    private static readonly Dictionary<string, DesRuleEntry> DesRules = new()
    {
        {"appId", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "uy7mzc4h", ObfuscatedName = "xx" }},
        {"box", new DesRuleEntry { IsEncrypt = 0, ObfuscatedName = "jf" }},
        {"canvas", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "snrn887t", ObfuscatedName = "yk" }},
        {"clientSize", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "cpmjjgsu", ObfuscatedName = "zx" }},
        {"organization", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "78moqjfc", ObfuscatedName = "dp" }},
        {"os", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "je6vk6t4", ObfuscatedName = "pj" }},
        {"platform", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "pakxhcd2", ObfuscatedName = "gm" }},
        {"plugins", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "v51m3pzl", ObfuscatedName = "kq" }},
        {"pmf", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "2mdeslu3", ObfuscatedName = "vw" }},
        {"protocol", new DesRuleEntry { IsEncrypt = 0, ObfuscatedName = "protocol" }},
        {"referer", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "y7bmrjlc", ObfuscatedName = "ab" }},
        {"res", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "whxqm2a7", ObfuscatedName = "hf" }},
        {"rtype", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "x8o2h2bl", ObfuscatedName = "lo" }},
        {"sdkver", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "9q3dcxp2", ObfuscatedName = "sc" }},
        {"status", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "2jbrxxw4", ObfuscatedName = "an" }},
        {"subVersion", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "eo3i2puh", ObfuscatedName = "ns" }},
        {"svm", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "fzj3kaeh", ObfuscatedName = "qr" }},
        {"time", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "q2t3odsk", ObfuscatedName = "nb" }},
        {"timezone", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "1uv05lj5", ObfuscatedName = "as" }},
        {"tn", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "x9nzj1bp", ObfuscatedName = "py" }},
        {"trees", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "acfs0xo4", ObfuscatedName = "pi" }},
        {"ua", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "k92crp1t", ObfuscatedName = "bj" }},
        {"url", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "y95hjkoo", ObfuscatedName = "cf" }},
        {"version", new DesRuleEntry { IsEncrypt = 0, ObfuscatedName = "version" }},
        {"vpw", new DesRuleEntry { Cipher = "DES", IsEncrypt = 1, Key = "r9924ab5", ObfuscatedName = "ca" }},
    };

    private static readonly Dictionary<string, object> DesTarget = new()
    {
        {"protocol", 102},
        {"organization", "UWXspnCCJN4sfYlNfqps"},
        {"appId", "default"},
        {"os", "web"},
        {"version", "3.0.0"},
        {"sdkver", "3.0.0"},
        {"box", ""},
        {"rtype", "all"},
        {"subVersion", "1.0.0"},
        {"time", 0},
    };

    private static readonly Dictionary<string, object> BrowserEnv = new()
    {
        {"plugins", "MicrosoftEdgePDFPluginPortableDocumentFormatinternal-pdf-viewer1,MicrosoftEdgePDFViewermhjfbmdgcfjbbpaeojofohoefgiehjai1"},
        {"ua", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36 Edg/129.0.0.0"},
        {"canvas", "259ffe69"},
        {"timezone", -480},
        {"platform", "Win32"},
        {"url", "https://www.skland.com/"},
        {"referer", ""},
        {"res", "1920_1080_24_1.25"},
        {"clientSize", "0_0_1080_1920_1920_1080_1920_1080"},
        {"status", "0011"},
    };

    private static HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    private static async Task<JsonDocument> Request(HttpMethod method, string url, Dictionary<string, string>? headers = null, object? jsonData = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (jsonData != null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(jsonData), Encoding.UTF8, "application/json");
        }

        var resp = await _client.SendAsync(request);
        resp.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status code is not 2xx

        var content = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private static byte[] DesEncrypt(byte[] key, byte[] data)
    {
        using (DES des = DES.Create())
        {
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.None; // Manual padding

            // Ensure key is 8 bytes
            byte[] key8 = new byte[8];
            Buffer.BlockCopy(key, 0, key8, 0, Math.Min(key.Length, 8));

            des.Key = key8;

            // Pad data to multiple of 8 bytes with null bytes
            int paddingLen = 8 - (data.Length % 8);
            byte[] paddedData = new byte[data.Length + paddingLen];
            Buffer.BlockCopy(data, 0, paddedData, 0, data.Length);

            using (var encryptor = des.CreateEncryptor())
            {
                return encryptor.TransformFinalBlock(paddedData, 0, paddedData.Length);
            }
        }
    }

    private static Dictionary<string, object> ApplyDesRules(Dictionary<string, object> data)
    {
        var result = new Dictionary<string, object>();
        foreach (var entry in data)
        {
            var key = entry.Key;
            var value = entry.Value;
            var strValue = value?.ToString() ?? "";

            if (DesRules.TryGetValue(key, out var rule))
            {
                if (rule.IsEncrypt == 1)
                {
                    var desKey = Encoding.UTF8.GetBytes(rule.Key);
                    var encrypted = DesEncrypt(desKey, Encoding.UTF8.GetBytes(strValue));
                    result[rule.ObfuscatedName] = Convert.ToBase64String(encrypted);
                }
                else
                {
                    result[rule.ObfuscatedName] = value;
                }
            }
            else
            {
                result[key] = value;
            }
        }
        return result;
    }

    private static string GetTn(Dictionary<string, object> data)
    {
        var sortedKeys = data.Keys.OrderBy(k => k).ToList();
        var result = new StringBuilder();
        foreach (var key in sortedKeys)
        {
            var value = data[key];
            if (value is int intValue)
            {
                result.Append(intValue * 10000);
            }
            else if (value is Dictionary<string, object> dictValue)
            {
                result.Append(GetTn(dictValue));
            }
            else
            {
                result.Append(value?.ToString() ?? "");
            }
        }
        return result.ToString();
    }

    private static string AesEncrypt(byte[] data, byte[] key)
    {
        using (Aes aes = Aes.Create())
        {
            aes.KeySize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7; // Python's pad function uses PKCS7

            byte[] iv = Encoding.UTF8.GetBytes("0102030405060708");
            aes.IV = iv;
            aes.Key = key;

            // Base64 encode the data first, then pad to multiple of 16
            var encodedB64 = Encoding.UTF8.GetBytes(Convert.ToBase64String(data));

            using (var encryptor = aes.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(encodedB64, 0, encodedB64.Length);
                }
                return BitConverter.ToString(ms.ToArray()).Replace("-", "").ToLower();
            }
        }
    }

    private static string GetSmid()
    {
        var timeStr = DateTime.Now.ToString("yyyyMMddHHmmss");
        var uid = Guid.NewGuid().ToString();
        using (MD5 md5 = MD5.Create())
        {
            var uidHash = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(uid))).Replace("-", "").ToLower();
            var v = $"{timeStr}{uidHash}00";
            var smskWebHash = md5.ComputeHash(Encoding.UTF8.GetBytes($"smsk_web_{v}"));
            var suffix = BitConverter.ToString(smskWebHash, 0, 7).Replace("-", "").ToLower();
            return $"{v}{suffix}0";
        }
    }

    public static async Task<string> GetDeviceId()
    {
        // Generate UUID and priId
        var uid = Guid.NewGuid().ToString();
        byte[] priIdHash;
        using (MD5 md5 = MD5.Create())
        {
            priIdHash = md5.ComputeHash(Encoding.UTF8.GetBytes(uid)).Take(8).ToArray();
        }
        var priIdHex = BitConverter.ToString(priIdHash).Replace("-", "").ToLower();

        // RSA encrypt UUID
        var publicKeyDer = Convert.FromBase64String(RsaPublicKey);
        using (RSA rsa = RSA.Create())
        {
            rsa.ImportSubjectPublicKeyInfo(publicKeyDer, out _);
            var encryptedUid = rsa.Encrypt(Encoding.UTF8.GetBytes(uid), RSAEncryptionPadding.Pkcs1);
            var epBase64 = Convert.ToBase64String(encryptedUid);

            // Build browser fingerprint
            long inMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var browser = new Dictionary<string, object>(BrowserEnv);
            browser["vpw"] = Guid.NewGuid().ToString();
            browser["trees"] = Guid.NewGuid().ToString();
            browser["svm"] = inMs;
            browser["pmf"] = inMs;

            // Build target data
            var desTarget = new Dictionary<string, object>(DesTarget);
            desTarget["smid"] = GetSmid();
            foreach (var entry in browser)
            {
                desTarget[entry.Key] = entry.Value;
            }

            // Generate tn
            var tnInput = GetTn(desTarget);
            using (MD5 md5 = MD5.Create())
            {
                desTarget["tn"] = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(tnInput))).Replace("-", "").ToLower();
            }

            // Apply DES rules and compress
            var desResult = ApplyDesRules(desTarget);
            var jsonStr = JsonSerializer.Serialize(desResult, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize))
                {
                    gzip.Write(Encoding.UTF8.GetBytes(jsonStr), 0, Encoding.UTF8.GetBytes(jsonStr).Length);
                }
                compressed = ms.ToArray();
            }

            // AES encrypt
            var encrypted = AesEncrypt(compressed, Encoding.UTF8.GetBytes(priIdHex));

            // Request device ID
            var response = await Request(
                HttpMethod.Post,
                "https://fp-it.portal101.cn/deviceprofile/v4",
                jsonData: new
                {
                    appId = "default",
                    compress = 2,
                    data = encrypted,
                    encode = 5,
                    ep = epBase64,
                    organization = "UWXspnCCJN4sfYlNfqps",
                    os = "web",
                }
            );

            if (response.RootElement.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() != 1100)
            {
                throw new Exception($"Device ID generation failed: {response.RootElement.ToString()}");
            }

            return $"B{response.RootElement.GetProperty("detail").GetProperty("deviceId").GetString()}";
        }
    }
}
