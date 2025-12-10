using System.Text;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole( loggingOptions =>
{
    loggingOptions.SingleLine = true;
    loggingOptions.TimestampFormat = "hh:mm:ss ";
});
var app = builder.Build();
var logger = app.Logger;

// OTLP/HTTP uses Protobuf over HTTP. The path usually defaults to /v1/metrics, /v1/traces, or /v1/logs.
// We will catch all to be safe and log the request details.
app.MapPost("/{*path}", async (HttpContext context, [FromHeader(Name = "Authorization")] string authorization) =>
{
    var path = context.Request.Path;
    var contentLength = context.Request.ContentLength;
    var (username, password) = ExtractBasicAuthCredentials(authorization);
    
    logger.LogInformation("Received OTLP request at {Path} | Authorization Header: {Authorization} | Decoded Credentials: {Username}/{Password} | Content-Length: {ContentLength}", 
        path, authorization ?? "none", username ?? "none", password ?? "none", contentLength);

    // Read the body to ensure we consume it (optional but good for debugging if needed)
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    // Console.WriteLine($"[OTLP Backend] Body length read: {body.Length}");

    return Results.Ok();
});

app.Run();

static (string? username, string? password) ExtractBasicAuthCredentials(string? authorization)
{
    if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return (null, null);
    }

    try
    {
        var base64Credentials = authorization.Substring("Basic ".Length).Trim();
        var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(base64Credentials));
        var parts = credentials.Split(':', 2);
        
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }
    }
    catch
    {
        // Silently return null for invalid auth
    }

    return (null, null);
}
