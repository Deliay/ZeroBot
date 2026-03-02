using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ZeroBot.Endfield.Api.Skland.Login;

public class LoginHelper
{
    private static readonly HttpClient Client = new();

    private static readonly HttpContent LoginBody = new StringContent("{\"appCode\":\"4ca99fa6b56cc2ba\"}",
        new MediaTypeHeaderValue("application/json"));

    private static async ValueTask<LoginQrCodeResponse> RequestLoginQrCode(CancellationToken cancellationToken = default)
    {
        var res = await Client.PostAsync("https://as.hypergryph.com/general/v1/gen_scan/login", LoginBody,
            cancellationToken);

        res.EnsureSuccessStatusCode();
        var result = await res.Content.ReadFromJsonAsync<Response<LoginQrCodeResponse>>(cancellationToken);
        result.EnsureSuccessStatusCode();
        return result.data;
    }
}