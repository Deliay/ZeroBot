using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Milky.Net.Model;
using ZeroBot.Abstraction;

namespace ZeroBot.Milky.Bot;

public class MilkyWebSocketReceiver(HttpClient client)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { AllowOutOfOrderMetadataProperties = true };
    
    public async IAsyncEnumerable<Event> ReadEvents([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            var uriBuilder = new UriBuilder(client.BaseAddress!);
            if (client.DefaultRequestHeaders.Authorization?.Parameter is { Length: > 0 } token)
            {
                uriBuilder.Query = $"access_token={token}";
            }
            await ws.ConnectAsync(uriBuilder.Uri, client, cancellationToken);
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
                ms.Seek(0L, SeekOrigin.Begin);
                var @event = JsonSerializer.Deserialize<Event>(ms, _jsonOptions);
                if (@event != null) yield return @event;
                ms.SetLength(0L);
            }
        }
    }
}