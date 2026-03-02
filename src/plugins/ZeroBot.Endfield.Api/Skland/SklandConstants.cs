using ZeroBot.Endfield.Api.Skland.Sign;

namespace ZeroBot.Endfield.Api.Skland;

public static class SklandConstants
{
    
    public static readonly IReadOnlyDictionary<string, DesRuleEntry> DesRules = new Dictionary<string, DesRuleEntry>
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

    public static readonly IReadOnlyDictionary<string, object> DesTarget = new Dictionary<string, object>
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

    public static readonly IReadOnlyDictionary<string, object> BrowserEnv = new Dictionary<string, object>
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

    public const string UserAgent = "Mozilla/5.0 (Linux; Android 12; SM-A5560 Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36; SKLand/1.52.1";
    public const string RsaPublicKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCmxMNr7n8ZeT0tE1R9j/mPixoinPkeM+k4VGIn/s0k7N5rJAfnZ0eMER+QhwFvshzo0LNmeUkpR8uIlU/GEVr8mN28sKmwd2gpygqj0ePnBmOW4v0ZVwbSYK+izkhVFk2V/doLoMbWy6b+UnA8mkjvg0iYWRByfRsK2gdl7llqCwIDAQAB";

}