using System.Text.Json;
using gmox.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigurateBuilder(builder);

var app = builder.Build();

ConfigurateSwagger(app);

app.UseHttpsRedirection();

Endpoints(app);
app.Run();

static void ConfigurateSwagger(WebApplication app)
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Definindo para o App que vamos utilizar webSockets
    app.UseWebSockets();
}

static void ConfigurateBuilder(WebApplicationBuilder builder)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSingleton<NotificationService>();
}


static void Endpoints(WebApplication app)
{
   app.MapGet("/registerSocket", async (HttpContext ctx, NotificationService notificationService) =>
    {
        if (ctx.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await ctx.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid().ToString();
            await notificationService.HandleWebSocketConnection(ctx, webSocket);
            await ctx.Response.WriteAsync($"WebSocket connected. ConnectionId: {connectionId}");
        }
        else
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync("Expected a WebSocket request");
        }
    });

    app.MapPost("/sendToGroup", async (string group, string message, NotificationService notificationService) =>
    {
        await notificationService.SendNotificationToGroup(group, message);
        return Results.Ok($"Notification sent to '{group}' group");
    });

   
}
