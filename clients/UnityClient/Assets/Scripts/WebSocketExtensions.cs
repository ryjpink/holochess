using Newtonsoft.Json;
using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class WebSocketExtensions
{
    public static async Task<T> ReceiveMessage<T>(this ClientWebSocket clientWebSocket, CancellationToken cancellationToken)
    {
        string json = await clientWebSocket.ReceiveMessage(cancellationToken);
        var message = JsonConvert.DeserializeObject<T>(json);
        if (message == null)
        {
            throw new IOException("Received invalid message");
        }
        return message;
    }

    public static async Task<string> ReceiveMessage(this ClientWebSocket clientWebSocket, CancellationToken cancellationToken)
    {
        using IMemoryOwner<byte> receiveBuffer = MemoryPool<byte>.Shared.Rent(4096);
        using var memoryStream = new MemoryStream();
        ValueWebSocketReceiveResult result;
        do
        {
            result = await clientWebSocket.ReceiveAsync(receiveBuffer.Memory, cancellationToken);
            memoryStream.Write(receiveBuffer.Memory.Span.Slice(0, result.Count));
        } while (!result.EndOfMessage);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            throw new IOException("End of stream");
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        string message = Encoding.UTF8.GetString(memoryStream.ToArray());
        return message;
    }

    public static async Task SendMessage(this ClientWebSocket clientWebSocket, string message, CancellationToken cancellationToken)
    {
        var bytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
        await clientWebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }
}