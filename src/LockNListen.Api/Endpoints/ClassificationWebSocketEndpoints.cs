using System.Net.WebSockets;
using System.Threading.Tasks;
using LockNListen.Api.WebSockets;
using LockNListen.Domain.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LockNListen.Api.Endpoints
{
    public static class ClassificationWebSocketEndpoints
    {
        public static void MapClassificationWebSocketEndpoints(this IEndpointRouteBuilder app)
        {
            app.Map("/api/classify/live", async (HttpContext context, ISoundClassifier classifier) =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                var handler = new ClassificationWebSocketHandler();
                await handler.HandleConnectionAsync(webSocket, classifier);
            })
            .WithName("ClassifyLive")
            .WithOpenApi();
        }
    }
}