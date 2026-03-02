namespace ZeroBot.Endfield.Api.Skland.Login;

public readonly record struct Response<T>(T data, string msg, int status, string type)
{
    public void EnsureSuccessStatusCode()
    {
        if (status != 0) throw new InvalidOperationException(msg);
    }
}

public readonly record struct LoginQrCode(string scanId, string scanUrl);

