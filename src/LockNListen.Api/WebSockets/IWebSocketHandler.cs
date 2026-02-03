using System.Net.WebSockets;
using System.Threading.Tasks;
using LockNListen.Domain.Services;

namespace LockNListen.Api.WebSockets
{
    public interface IWebSocketHandler
    {
        Task HandleConnectionAsync(WebSocket webSocket, ISoundClassifier classifier);
    }
}