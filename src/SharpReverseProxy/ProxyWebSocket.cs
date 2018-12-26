using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpReverseProxy
{
    class ProxyWebSocket
    {
        public static async Task HandleWebSocket(HttpContext context, HttpRequestMessage proxyRequest, ProxyRule proxyRule)
        {
            var outerWebSocket = await context.WebSockets.AcceptWebSocketAsync();
            ClientWebSocket innerWebSocket = new ClientWebSocket();
            var uri = new UriBuilder(proxyRequest.RequestUri)
            {
                Scheme = "ws"
            };
            await innerWebSocket.ConnectAsync(uri.Uri, CancellationToken.None);
            var forwardIncomingTask = ForwardIncoming(outerWebSocket, innerWebSocket);
            var forwardOutgoingTask = ForwardOutgoing(innerWebSocket, outerWebSocket);
            await forwardIncomingTask;
            await forwardOutgoingTask;
        }

        private static async Task ForwardIncoming(WebSocket outerWebSocket, ClientWebSocket innerWebSocket)
        {
            try
            {
                var buffer = new byte[4096];
                WebSocketReceiveResult receiveResult;
                while (true)
                {
                    receiveResult = await outerWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var segment = new ArraySegment<byte>(buffer, 0, receiveResult.Count);
                    await innerWebSocket.SendAsync(segment, receiveResult.MessageType, receiveResult.EndOfMessage, CancellationToken.None); //TODO handle close
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ForwardIncoming failed: {e.Message}\n{e.StackTrace}");
            }
        }

        private static async Task ForwardOutgoing(ClientWebSocket ws, WebSocket outerWebSocket)
        {
            try
            {
                var message = new List<byte>();
                var buffer = new byte[4096];
                WebSocketReceiveResult receiveResult;
                while (true)
                {
                    receiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await outerWebSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, CancellationToken.None);
                        break;
                    }
                    var segment = new ArraySegment<byte>(buffer, 0, receiveResult.Count);
                    await outerWebSocket.SendAsync(segment, receiveResult.MessageType, receiveResult.EndOfMessage, CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ForwardOutgoing failed: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
