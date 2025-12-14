using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Milky.Net.Model;
using ZeroBot.Abstraction;

namespace ZeroBot.Milky.Bot;

public class MilkyWebSocketReceiver(HttpClient client)
{
    private static readonly Uri EventRelativeUri = new("/event");
    public async IAsyncEnumerable<Event> ReadEvents([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(client.BaseAddress!, EventRelativeUri), client, cancellationToken);
        using var ms = new MemoryStream();
        while (!cancellationToken.IsCancellationRequested)
        {
            var rawBuffer = ArrayPool<byte>.Shared.Rent(4096);
            Memory<byte> buffer = rawBuffer;
            try
            {
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, cancellationToken);
                    await ms.WriteAsync(buffer[..result.Count], cancellationToken);
                } while (!result.EndOfMessage);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rawBuffer);
            }

            var @event = await JsonSerializer.DeserializeAsync<Event>(ms, MilkyJsonSerializerContext.Default.Event,
                cancellationToken);
            if (@event != null) yield return @event;
            ms.SetLength(0L);
        }
    }
}