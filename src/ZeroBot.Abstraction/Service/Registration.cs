namespace ZeroBot.Abstraction.Service;

public class Registration(Action unregister) : IDisposable
{
    public void Dispose()
    {
        unregister();
    }
} 