namespace ZeroBot.Endfield.Api.Skland;

public readonly record struct Response<T>(T data, string msg, int status, string type)
{
    public void EnsureSuccessStatusCode()
    {
        if (status is 0 or (>= 200 and < 300)) return;
        
        throw new InvalidOperationException(msg);
    }
}

public readonly record struct ZonResponse<T>(T data, string message, int code)
{
    
    public void EnsureSuccessStatusCode()
    {
        if (code is 0 or (>= 200 and < 300)) return;
        
        throw new InvalidOperationException(message);
    }
}
