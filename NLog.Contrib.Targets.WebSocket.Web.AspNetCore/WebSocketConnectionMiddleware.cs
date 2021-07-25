﻿using Microsoft.AspNetCore.Http;
using Owin.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NLog.Contrib.Targets.WebSocket.Web.AspNetCore
{
    public class WebSocketConnectionMiddleware<T> : IMiddleware
        where T : WebSocketConnection
    {
        private readonly Regex mMatchPattern;
        private readonly IServiceProvider _serviceProvider;

        private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        public WebSocketConnectionMiddleware(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public WebSocketConnectionMiddleware(IServiceProvider serviceProvider,
            Regex matchPattern)
            : this(serviceProvider)
        {
            mMatchPattern = matchPattern;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var requestPath = context.Request.Path.Value;
            if (!requestPath.StartsWith(Constants.PATH_WSLOGGER, StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
            }
            // serve viewer
            else if (requestPath.StartsWith(Constants.PATH_VIEWER, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await ServeViewerStatic(context, requestPath);
                }
                catch (FileNotFoundException)
                {
                    await next(context);
                }
            }
            else if (requestPath.StartsWith(Constants.PATH_WSLOGGER_LISTENER, StringComparison.OrdinalIgnoreCase))
            {
                var routeAttributes = typeof(T).GetCustomAttributes(typeof(WebSocketRouteAttribute), true).Cast<WebSocketRouteAttribute>();
                if (routeAttributes.Count() == 0)
                {
                    await next(context);
                }
                else
                {
                    var matches = FindMatches(requestPath, routeAttributes);
                    if (matches.Count == 0)
                    {
                        await next(context);
                    }
                    else
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            T socketConnection;
                            if (_serviceProvider == null)
                                socketConnection = Activator.CreateInstance<T>();
                            else
                                socketConnection = _serviceProvider.GetService<T>();

                            await socketConnection.AcceptSocketAsync(context, matches);
                        }
                    }
                }
            }
        }

        private async Task ServeViewerStatic(HttpContext context, string requestPath)
        {
            if (requestPath.EndsWith(Constants.PATH_VIEWER))
            {
                requestPath += Constants.VIEWER_INDEX;
            }
            var filePath = Path.Combine(new List<string> { BaseDirectory }.Concat(requestPath.Replace(Constants.PATH_VIEWER, Constants.VIEWER_DIR).Trim('/', ' ').Replace("//", "/").Split("/")).ToArray());

            var contentType = filePath.EndsWith(Constants.EXTENSION_HTML)
                               ? Constants.MEDIATYPE_HTML
                               : filePath.EndsWith(Constants.EXTENSION_JS)
                               ? Constants.MEDIATYPE_JS
                               : filePath.EndsWith(Constants.EXTENSION_CSS)
                               ? Constants.MEDIATYPE_CSS
                               : throw new FileNotFoundException();
            if (File.Exists(filePath))
            {
                context.Response.ContentType = contentType;
                await context.Response.WriteAsync(await File.ReadAllTextAsync(filePath), Encoding.UTF8);
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        private static Dictionary<string, string> FindMatches(string requestPath, IEnumerable<WebSocketRouteAttribute> routeAttributes)
        {
            var matches = new Dictionary<string, string>();
            foreach (var routeAttribute in routeAttributes)
            {
                var matchPattern = new Regex(routeAttribute.Route);

                var match = matchPattern.Match(requestPath);
                if (!match.Success)
                    continue;

                for (var i = 1; i <= match.Groups.Count; i++)
                {
                    var name = matchPattern.GroupNameFromNumber(i);
                    var value = match.Groups[i];
                    matches.Add(name, value.Value);
                }
            }

            return matches;
        }
    }
}
