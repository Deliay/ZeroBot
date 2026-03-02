using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Polly;
using Polly.Retry;

namespace ZeroBot.Endfield.Api.Skland.Sign;

public class SklandSignHelper(DeviceIdManager deviceIdManager, int maxRetries = 3) : IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ResiliencePipeline _retry = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions() { MaxRetryAttempts = maxRetries }).Build();

    private static bool IsSignedToday(SignInResult result)
    {
        if (result.Success) return true;

        var error = result.Error?.ToLower() ?? "";
        return new[] { "已签到", "请勿重复", "重复签到", "already", "签到过", "今日已" }
            .Any(keyword => error.Contains(keyword));
    }

    private async Task<JsonDocument> Request(
        HttpMethod method,
        string url,
        Dictionary<string, string>? headers = null, 
        object? jsonData = null, CancellationToken cancellationToken = default)
    {
        return (await _retry.ExecuteAsync(async (token) =>
        {
            var request = new HttpRequestMessage(method, url);
            foreach (var header in headers ?? [])
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            if (jsonData is not null) request.Content = new StringContent(JsonSerializer.Serialize(jsonData), Encoding.UTF8, "application/json");
            
            var resp = await _client.SendAsync(request, token);
            return await resp.EnsureSuccessStatusCode()
                .Content
                .ReadFromJsonAsync<JsonDocument>(token);
        }, cancellationToken))!;
    }
    
    private static Dictionary<string, string> GetBaseHeaders(string did)
    {
        return new Dictionary<string, string>
        {
            {"User-Agent", SklandConstants.UserAgent},
            {"Accept-Encoding", "gzip"},
            {"Connection", "close"},
            {"X-Requested-With", "com.hypergryph.skland"},
            {"dId", did},
        };
    }

    private async Task<string> GetAuthorization(string userToken, CancellationToken cancellationToken = default)
    {
        var did = await deviceIdManager.GenerateDeviceId(cancellationToken);
        var headers = GetBaseHeaders(did);

        var response = await Request(
            HttpMethod.Post,
            "https://as.hypergryph.com/user/oauth2/v2/grant",
            headers: headers,
            jsonData: new { appCode = "4ca99fa6b56cc2ba", token = userToken, type = 0 }
        );

        if (!response.RootElement.TryGetProperty("status", out var statusElement) || statusElement.GetInt32() == 0)
            return response.RootElement.GetProperty("data").GetProperty("code").GetString()!;
        
        var message = response.RootElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error";
        throw new Exception($"Authorization failed: {message}");

    }

    private async Task<Credential> GetCredential(string authorization, CancellationToken cancellationToken = default)
    {
        var did = await deviceIdManager.GenerateDeviceId(cancellationToken);
        var headers = GetBaseHeaders(did);

        var response = await Request(
            HttpMethod.Post,
            "https://zonai.skland.com/web/v1/user/auth/generate_cred_by_code",
            headers: headers,
            jsonData: new { code = authorization, kind = 1 }
        );

        if (response.RootElement.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() != 0)
        {
            var message = response.RootElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error";
            throw new Exception($"Credential failed: {message}");
        }

        var data = response.RootElement.GetProperty("data");
        return new Credential
        {
            Token = data.GetProperty("token").GetString()!,
            Cred = data.GetProperty("cred").GetString()!,
        };
    }

    private static Dictionary<string, string> GetSignedHeaders(string url, HttpMethod method, string? body, Credential cred, string did)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;
        var query = uri.Query.TrimStart('?');

        string sign;
        Dictionary<string, string> headerCa;

        if (method == HttpMethod.Get)
        {
            (sign, headerCa) = SklandEncryption.GenerateSignature(cred.Token, path, query, did);
        }
        else
        {
            (sign, headerCa) = SklandEncryption.GenerateSignature(cred.Token, path, body ?? "", did);
        }

        var headers = GetBaseHeaders(did);
        headers["cred"] = cred.Cred;
        headers["sign"] = sign;
        foreach (var entry in headerCa)
        {
            headers[entry.Key] = entry.Value;
        }

        return headers;
    }

    private async Task<List<UserBinding>> GetBindingList(Credential cred, CancellationToken cancellationToken = default)
    {
        var did = await deviceIdManager.GenerateDeviceId(cancellationToken);
        const string url = "https://zonai.skland.com/api/v1/game/player/binding";
        var headers = GetSignedHeaders(url, HttpMethod.Get, null, cred, did);

        var response = await Request(HttpMethod.Get, url, headers: headers);

        if (response.RootElement.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() != 0)
        {
            var msg = response.RootElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error";
            if (msg == "用户未登录")
            {
                throw new Exception("用户登录已过期，请重新登录");
            }
            throw new Exception($"获取绑定列表失败: {msg}");
        }

        var bindings = new List<UserBinding>();
        if (!response.RootElement.TryGetProperty("data", out var dataElement) ||
            !dataElement.TryGetProperty("list", out var listElement) ||
            listElement.ValueKind != JsonValueKind.Array) return bindings;

        foreach (var item in listElement.EnumerateArray())
        {
            var appCode = item.TryGetProperty("appCode", out var acElement) ? acElement.GetString() : "";
            if (appCode != "arknights" && appCode != "endfield")
            {
                continue;
            }

            if (item.TryGetProperty("bindingList", out var bindingListElement) && bindingListElement.ValueKind == JsonValueKind.Array)
            {
                bindings.AddRange(bindingListElement.EnumerateArray()
                    .Select(bindingItem => new UserBinding
                    {
                        AppCode = appCode,
                        GameName = bindingItem.TryGetProperty("gameName", out var gnElement) ? gnElement.GetString()! : "Unknown",
                        Nickname = bindingItem.TryGetProperty("nickName", out var nnElement) ? nnElement.GetString()! : "Unknown",
                        ChannelName = bindingItem.TryGetProperty("channelName", out var cnElement) ? cnElement.GetString()! : "Unknown",
                        Uid = bindingItem.TryGetProperty("uid", out var uidElement) ? uidElement.GetString()! : "",
                        GameId = bindingItem.TryGetProperty("gameId", out var giElement) ? giElement.GetInt32() : 1,
                        Roles = bindingItem.TryGetProperty("roles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array
                            ? rolesElement.EnumerateArray().Select(r => r.Deserialize<Dictionary<string, object>>()!).ToList()
                            : []
                    }));
            }
        }
        return bindings;
    }

    public async Task<SignInResult> SignArknights(Credential cred, UserBinding binding, CancellationToken cancellationToken = default)
    {
        var did = await deviceIdManager.GenerateDeviceId(cancellationToken);
        const string url = "https://zonai.skland.com/api/v1/game/attendance";
        var bodyData = new { gameId = binding.GameId, uid = binding.Uid };
        var body = JsonSerializer.Serialize(bodyData, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var headers = GetSignedHeaders(url, HttpMethod.Post, body, cred, did);

        var response = await Request(
            HttpMethod.Post,
            url,
            headers: headers,
            jsonData: bodyData
        );

        Debug.WriteLine($"[明日方舟] {binding.Nickname} sign-in response: {response.RootElement.ToString()}");

        if (response.RootElement.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() != 0)
        {
            return new SignInResult
            {
                Success = false,
                Game = "明日方舟",
                Nickname = binding.Nickname,
                Channel = binding.ChannelName,
                Error = response.RootElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error"
            };
        }

        var awards = new List<string>();
        if (!response.RootElement.TryGetProperty("data", out var dataElement) ||
            !dataElement.TryGetProperty("awards", out var awardsElement) ||
            awardsElement.ValueKind != JsonValueKind.Array)
            return new SignInResult
            {
                Success = true,
                Game = "明日方舟",
                Nickname = binding.Nickname,
                Channel = binding.ChannelName,
                Awards = awards
            };
        foreach (var award in awardsElement.EnumerateArray())
        {
            var name = award.TryGetProperty("resource", out var resourceElement) && resourceElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "Unknown";
            var count = award.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 1;
            awards.Add($"{name}x{count}");
        }

        return new SignInResult
        {
            Success = true,
            Game = "明日方舟",
            Nickname = binding.Nickname,
            Channel = binding.ChannelName,
            Awards = awards
        };
    }

    public async Task<List<SignInResult>> SignEndfield(Credential cred, UserBinding binding, CancellationToken cancellationToken = default)
    {
        var results = new List<SignInResult>();
        var roles = binding.Roles;

        if (!roles.Any())
        {
            results.Add(new SignInResult
            {
                Success = false,
                Game = "终末地",
                Nickname = binding.Nickname,
                Channel = binding.ChannelName,
                Error = "没有角色数据"
            });
            return results;
        }

        var did = await deviceIdManager.GenerateDeviceId(cancellationToken);
        const string url = "https://zonai.skland.com/web/v1/game/endfield/attendance";

        foreach (var role in roles)
        {
            var roleNickname = role.TryGetValue("nickname", out var nnVal) ? nnVal.ToString()! : binding.Nickname;
            var roleId = role.TryGetValue("roleId", out var riVal) ? riVal.ToString() : "";
            var serverId = role.TryGetValue("serverId", out var siVal) ? siVal.ToString() : "";

            var headers = GetSignedHeaders(url, HttpMethod.Post, "", cred, did);
            headers["Content-Type"] = "application/json";
            headers["sk-game-role"] = $"3_{roleId}_{serverId}";
            headers["referer"] = "https://game.skland.com/";
            headers["origin"] = "https://game.skland.com/";

            
            JsonDocument response;
            try
            {
                response = await Request(HttpMethod.Post, url, headers);
            }
            catch (Exception e)
            {
                results.Add(new SignInResult
                {
                    Success = false,
                    Game = "终末地",
                    Nickname = roleNickname,
                    Channel = binding.ChannelName,
                    Error = e.Message
                });
                continue;
            }

            if (response.RootElement.TryGetProperty("code", out var codeElement) && codeElement.GetInt32() != 0)
            {
                results.Add(new SignInResult
                {
                    Success = false,
                    Game = "终末地",
                    Nickname = roleNickname,
                    Channel = binding.ChannelName,
                    Error = response.RootElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error"
                });
                continue;
            }

            var awards = new List<string>();
            if (response.RootElement.TryGetProperty("data", out var dataElement))
            {
                var awardIds = dataElement.TryGetProperty("awardIds", out var awardIdsElement) && awardIdsElement.ValueKind == JsonValueKind.Array
                    ? awardIdsElement.EnumerateArray().ToList()
                    : new List<JsonElement>();
                var resourceMap = dataElement.TryGetProperty("resourceInfoMap", out var resourceMapElement) && resourceMapElement.ValueKind == JsonValueKind.Object
                    ? resourceMapElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value)
                    : new Dictionary<string, JsonElement>();

                foreach (var award in awardIds)
                {
                    var aid = award.TryGetProperty("id", out var idElement) ? idElement.GetString() : "";
                    if (string.IsNullOrEmpty(aid) || !resourceMap.TryGetValue(aid, out var info)) continue;
                    
                    var name = info.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "Unknown";
                    var count = info.TryGetProperty("count", out var countElement) ? countElement.GetInt32() : 1;
                    awards.Add($"{name}x{count}");
                }
            }

            results.Add(new SignInResult
            {
                Success = true,
                Game = "终末地",
                Nickname = roleNickname,
                Channel = binding.ChannelName,
                Awards = awards
            });
        }

        return results;
    }

    public async Task<(List<SignInResult> results, string nickname)> DoFullSignIn(string userToken, CancellationToken cancellationToken = default)
    {
        // Get authorization
        var authCode = await GetAuthorization(userToken, cancellationToken);

        // Get credential
        var cred = await GetCredential(authCode, cancellationToken);

        // Get bindings
        var bindings = await GetBindingList(cred, cancellationToken);

        if (!bindings.Any())
        {
            return (new List<SignInResult>(), "");
        }

        var nickname = bindings.FirstOrDefault()?.Nickname ?? "";
        var results = new List<SignInResult>();

        foreach (var binding in bindings)
        {
            if (binding.AppCode == "arknights")
            {
                var result = await SignArknights(cred, binding, cancellationToken);
                results.Add(result);
            }
            else if (binding.AppCode == "endfield")
            {
                var endfieldResults = await SignEndfield(cred, binding, cancellationToken);
                results.AddRange(endfieldResults);
            }
        }

        return (results, nickname);
    }

    public async Task<(Dictionary<string, bool> status, string nickname)> CheckSignInStatus(string userToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var (results, nickname) = await DoFullSignIn(userToken, cancellationToken);

            var status = new Dictionary<string, bool> { { "arknights", false }, { "endfield", false } };

            foreach (var r in results)
            {
                switch (r.Game)
                {
                    case "明日方舟":
                        status["arknights"] = IsSignedToday(r);
                        break;
                    case "终末地":
                        status["endfield"] = IsSignedToday(r);
                        break;
                }
            }

            return (status, nickname);
        }
        catch (Exception)
        {
            return (new Dictionary<string, bool> { { "arknights", false }, { "endfield", false } }, "");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}