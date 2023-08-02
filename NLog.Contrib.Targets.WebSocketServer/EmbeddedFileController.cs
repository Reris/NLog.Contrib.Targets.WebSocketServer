﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NLog.Contrib.Targets.WebSocketServer;

public class EmbeddedFileController
{
    private static readonly (string File, string Mime)[] ContentTypes =
    {
        (".html", "text/html; charset=utf-8"),
        (".js", "application/javascript; charset=utf-8"),
        (".css", "text/css; charset=utf-8")
    };

    public static async Task RespondAsync(string embeddedFolder, HttpContext context, string requestPath)
    {
        var resourceSuffix = requestPath.Split('/').Last();
        var contentType = EmbeddedFileController.ContentTypes.FirstOrDefault(a => requestPath.EndsWith(a.File)).Mime
                          ?? throw new FileNotFoundException();

        var resourseId = $"{typeof(EmbeddedFileController).Namespace}.{embeddedFolder}.{resourceSuffix}";

        try
        {
            await using var stream = typeof(EmbeddedFileController).Assembly.GetManifestResourceStream(resourseId) ?? throw new FileNotFoundException();
            using var sr = new StreamReader(stream);
            var content = await sr.ReadToEndAsync();
            context.Response.ContentType = contentType;
            await context.Response.WriteAsync(content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceSuffix}' not found.", ex);
        }
    }
}