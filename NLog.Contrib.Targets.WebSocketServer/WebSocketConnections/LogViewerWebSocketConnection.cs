﻿using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using NLog.Targets.Wrappers;

namespace NLog.Contrib.Targets.WebSocketServer.WebSocketConnections;

public class LogViewerWebSocketConnection : WebSocketConnection
{
    public override async Task OnMessageReceived(ArraySegment<byte> message, WebSocketMessageType type)
    {
        if (message.Array is null)
        {
            return;
        }

        var cMessage = Encoding.UTF8.GetString(message.Array, message.Offset, message.Count);

        foreach (var target in LogViewerWebSocketConnection.GetWebSocketTargets())
        {
            await target.AcceptWebSocketCommands(cMessage, this.WebSocket);
        }
    }

    public override void OnOpen()
    {
        foreach (var target in LogViewerWebSocketConnection.GetWebSocketTargets())
        {
            target.Subscribe(this.WebSocket);
        }
    }

    public override void OnClose(WebSocketCloseStatus? closeStatus, string? closeStatusDescription)
    {
        foreach (var target in LogViewerWebSocketConnection.GetWebSocketTargets())
        {
            target.Unsubscribe(this.WebSocket);
        }
    }

    private static IEnumerable<WebSocketTarget> GetWebSocketTargets()
    {
        foreach (var target in LogManager.Configuration.ConfiguredNamedTargets)
        {
            switch (target)
            {
                case WebSocketTarget webSocketServerTarget:
                    yield return webSocketServerTarget;
                    break;
                case WrapperTargetBase wrapper:
                    while (wrapper.WrappedTarget is WrapperTargetBase deeper)
                    {
                        wrapper = deeper;
                    }

                    if (wrapper.WrappedTarget is WebSocketTarget wrappedWebSocketServerTarget)
                    {
                        yield return wrappedWebSocketServerTarget;
                    }

                    break;
            }
        }
    }
}