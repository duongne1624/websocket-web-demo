using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// URL khởi tạo ban đầu(Giữ nguyên để sử dụng message hoặc custom)
builder.WebHost.UseUrls("https://localhost:6969");

var app = builder.Build();

// Tạo Dictionary để lưu trữ WebSocket: Các kết nối & Phòng
var rooms = new Dictionary<string, List<WebSocket>>();

app.UseWebSockets();

app.Map("/ws", async context => {
    if (context.WebSockets.IsWebSocketRequest)
    {
        var roomName = context.Request.Query["room"];

        var curName = context.Request.Query["name"];

        // Kiểm tra thông tin gửi đến (không được để trống)
        if (string.IsNullOrEmpty(roomName))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("Room name is required.");
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        // Thêm kết nối vào phòng
        if (!rooms.ContainsKey(roomName))
        {
            rooms[roomName] = new List<WebSocket>();
        }
        rooms[roomName].Add(ws);

        await Broadcast(roomName, CreateMessage("System", $"{curName} joined the room"));

        await ReceiveMessage(ws, async (result, buffer) =>
        {
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Hành động khi người dùng gửi tin nhắn
                try
                {
                    var incomingMessage = JsonSerializer.Deserialize<Message>(message);

                    //Sử dụng lệnh trên server
                    if (incomingMessage.message.Equals("/people", StringComparison.OrdinalIgnoreCase))
                    {
                        var userCount = rooms[roomName].Count;
                        await Broadcast(roomName, CreateMessage(incomingMessage.sender, incomingMessage.message));
                        await Broadcast(roomName, CreateMessage("System", $"There are {userCount} users in the room."));
                        return;
                    }

                    // Chuyển đổi, phân tích tin nhắn gửi đến
                    if (incomingMessage != null)
                    {
                        // Thông báo gửi đến cho tất cả người dùng trong phòng đó
                        await Broadcast(roomName, CreateMessage(incomingMessage.sender, incomingMessage.message));
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error parsing message: {ex.Message}");
                }
            }
            else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
            {
                rooms[roomName].Remove(ws);
                await Broadcast(roomName, CreateMessage("System", $"{curName} left the room"));
                await ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
        });
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

// Nhận tin nhắn từ kết nối
async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
    }
}

// Tạo tin nhắn
string CreateMessage(string sender, string message)
{
    var msgObject = new { sender, message };
    return JsonSerializer.Serialize(msgObject);
}

// Gửi tin nhắn đế các kết nối
async Task Broadcast(string roomName, string message)
{
    if (!rooms.ContainsKey(roomName)) return;

    var bytes = Encoding.UTF8.GetBytes(message);
    var tasks = new List<Task>();

    foreach (var socket in rooms[roomName])
    {
        if (socket.State == WebSocketState.Open)
        {
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            tasks.Add(socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None));
        }
    }

    await Task.WhenAll(tasks);
}

await app.RunAsync();

public class Message
{
    public string sender { get; set; }
    public string message { get; set; }
}
