using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using System.Web;

namespace gmox.Services;

public class NotificationService
{
    private class WebSocketConnection
    {
        public WebSocket WebSocket { get; set; }
        public HashSet<string> Groups { get; set; } = new HashSet<string>();
    }

    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();

     public async Task HandleWebSocketConnection(HttpContext context, WebSocket webSocket)
    {
        var connectionId = Guid.NewGuid().ToString();
        var connection = new WebSocketConnection { WebSocket = webSocket };
        _connections[connectionId] = connection;

        var query = HttpUtility.ParseQueryString(context.Request.QueryString.ToString());
        var group = query["group"];

        if (!string.IsNullOrEmpty(group))
        {
            group = group.Trim('"');
            connection.Groups.Add(group);
        }

        var buffer = new byte[1024 * 4];
        try
        {
            var confirmationMessage = $"Connected to group: {group}";
            await SendMessageToWebSocket(webSocket, confirmationMessage);

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, default);
                    break;
                }
            }
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
        }
    }

    public void AddToGroup(string connectionId, string group)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.Groups.Add(group);
        }
    }

    public void RemoveFromGroup(string connectionId, string group)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.Groups.Remove(group);
        }
    }

    public async Task SendNotificationToGroup(string group, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var arraySegment = new ArraySegment<byte>(bytes);

        var tasks = _connections.Values
            .Where(c => c.Groups.Contains(group) && c.WebSocket.State == WebSocketState.Open)
            .Select(c => c.WebSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, default));

        await Task.WhenAll(tasks);
    }
    private async Task SendMessageToWebSocket(WebSocket webSocket, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var arraySegment = new ArraySegment<byte>(bytes);
        await webSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, default);
    }
}