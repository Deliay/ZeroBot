using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZeroBot.Endfield.Api.Skland.Sign;

public static class SklandEncryption
{
    private static byte[] DesEncrypt(byte[] key, byte[] data)
    {
        using var des = DES.Create();
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None; // Manual padding

        // Ensure key is 8 bytes
        var key8 = new byte[8];
        Buffer.BlockCopy(key, 0, key8, 0, Math.Min(key.Length, 8));

        des.Key = key8;

        // Pad data to multiple of 8 bytes with null bytes
        var paddingLen = 8 - (data.Length % 8);
        var paddedData = new byte[data.Length + paddingLen];
        Buffer.BlockCopy(data, 0, paddedData, 0, data.Length);

        using var encryptor = des.CreateEncryptor();
        return encryptor.TransformFinalBlock(paddedData, 0, paddedData.Length);
    }

    public static Dictionary<string, object> ApplyDesRules(Dictionary<string, object> data)
    {
        var result = new Dictionary<string, object>();
        foreach (var (key, value) in data)
        {
            var strValue = value?.ToString() ?? "";

            if (!SklandConstants.DesRules.TryGetValue(key, out var rule))
            {
                result[key] = value!;
                continue;
            }
            
            if (rule.IsEncrypt == 1)
            {
                var desKey = Encoding.UTF8.GetBytes(rule.Key!);
                var encrypted = DesEncrypt(desKey, Encoding.UTF8.GetBytes(strValue));
                result[rule.ObfuscatedName] = Convert.ToBase64String(encrypted);
            }
            else
            {
                result[rule.ObfuscatedName] = value!;
            }
        }
        return result;
    }

    public static string GetTn(Dictionary<string, object> data)
    {
        var sortedKeys = data.Keys.OrderBy(k => k).ToList();
        var result = new StringBuilder();
        foreach (var value in sortedKeys.Select(key => data[key]))
        {
            switch (value)
            {
                case int intValue:
                    result.Append(intValue * 10000);
                    break;
                case Dictionary<string, object> dictValue:
                    result.Append(GetTn(dictValue));
                    break;
                default:
                    result.Append(value?.ToString() ?? "");
                    break;
            }
        }
        return result.ToString();
    }

    public static string AesEncrypt(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7; // Python's pad function uses PKCS7

        aes.IV = "0102030405060708"u8.ToArray();
        aes.Key = key;

        // Base64 encode the data first, then pad to multiple of 16
        var encodedB64 = Encoding.UTF8.GetBytes(Convert.ToBase64String(data));

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(encodedB64, 0, encodedB64.Length);
        }
        return BitConverter.ToString(ms.ToArray()).Replace("-", "").ToLower();
    }

    public static string GetSmid()
    {
        var timeStr = DateTime.Now.ToString("yyyyMMddHHmmss");
        var uid = Guid.NewGuid().ToString();
        using var md5 = MD5.Create();
        var uidHash = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(uid))).Replace("-", "").ToLower();
        var v = $"{timeStr}{uidHash}00";
        var smskWebHash = md5.ComputeHash(Encoding.UTF8.GetBytes($"smsk_web_{v}"));
        var suffix = BitConverter.ToString(smskWebHash, 0, 7).Replace("-", "").ToLower();
        return $"{v}{suffix}0";
    }

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
        var headerCaStr = JsonSerializer.Serialize(headerCa, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        var s = $"{path}{bodyOrQuery}{timestamp}{headerCaStr}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(token));
        var hmacResult = hmac.ComputeHash(Encoding.UTF8.GetBytes(s));
        var hmacHex = BitConverter.ToString(hmacResult).Replace("-", "").ToLower();

        using var md5 = MD5.Create();
        var sign = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(hmacHex))).Replace("-", "").ToLower();

        return (sign, headerCa);
    }

}