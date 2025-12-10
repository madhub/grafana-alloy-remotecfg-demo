using Collector.V1;
using Google.Protobuf;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole( loggingOptions =>
{
    loggingOptions.SingleLine = true;
    loggingOptions.TimestampFormat = "hh:mm:ss ";
});
// Add services to the container.
builder.Services.AddGrpc(); // Keep this for the proto classes generation

var app = builder.Build();
var logger = app.Logger;

// Configure the HTTP request pipeline.
// app.UsePathBase("/config"); // Removing this to avoid confusion

app.MapPost("/collector.v1.CollectorService/RegisterCollector", async (HttpContext context) =>
{
    using var ms = new MemoryStream();
    await context.Request.Body.CopyToAsync(ms);
    var requestBytes = ms.ToArray();

    var request = RegisterCollectorRequest.Parser.ParseFrom(requestBytes);
    var response = new RegisterCollectorResponse();
    var responseBytes = response.ToByteArray();

    logger.LogInformation("RegisterCollector: ID={CollectorId}, ResponseSize={ResponseSize} bytes", request.Id, responseBytes.Length);

    context.Response.Headers.Append("Connect-Protocol-Version", "1");
    context.Response.Headers.Append("Grpc-Status", "0");
    return Results.Bytes(responseBytes, "application/proto");
});

app.MapGet("/collector.v1.CollectorService/GetConfig", async (HttpContext context) =>
{
    // For Connect GET, the message is in 'message' query parameter
    byte[] requestBytes = Array.Empty<byte>();
    if (context.Request.Query.ContainsKey("message"))
    {
        var msg = context.Request.Query["message"].ToString();
        try 
        {
            // Connect uses URL-safe base64. Standard FromBase64String might need padding adjustments
            // but for now we try direct. In a production app, handle Base64Url.
            // Simplified for this demo as we mainly need the ID which acts as key.
            // If parsing fails for valid input, we might need a better Base64Url Decoder.
            // However, the ID is inside the proto message.
            requestBytes = Convert.FromBase64String(msg.Replace('-', '+').Replace('_', '/').PadRight(4 * ((msg.Length + 3) / 4), '='));
        } 
        catch 
        {
             // Silently continue with empty request
        }
    }

    // Parse the request
    var request = GetConfigRequest.Parser.ParseFrom(requestBytes);

    string username;
    string password;

    switch (request.Id)
    {
        case "service-alpha":
            username = $"{Random.Shared.GetHexString(10,true)}";
            password = $"{Random.Shared.GetHexString(20)}";
            break;
        case "service-beta":
            username = $"{Random.Shared.GetHexString(10,true)}";
            password = $"{Random.Shared.GetHexString(20)}";
            break;
        case "service-a":
             username = $"{Random.Shared.GetHexString(10,true)}";
            password = $"{Random.Shared.GetHexString(20)}";
            break;            
        default:
            username = $"{Random.Shared.GetHexString(10,true)}";
            password = $"{Random.Shared.GetHexString(20)}";
            break;
    }

    var config = $@"
otelcol.auth.basic ""default"" {{
    username = ""{username}""
    password = ""{password}""
}}

otelcol.exporter.otlphttp ""default"" {{
    client {{
        endpoint = ""http://localhost:5318""
        auth     = otelcol.auth.basic.default.handler
        tls {{
            insecure = true
        }}
    }}
}}

otelcol.receiver.otlp ""default"" {{
  grpc {{
    endpoint = ""0.0.0.0:4317""
  }}
  http {{
    endpoint = ""0.0.0.0:4318""
  }}

  output {{
    metrics = [otelcol.exporter.otlphttp.default.input]
    logs    = [otelcol.exporter.otlphttp.default.input]
    traces  = [otelcol.exporter.otlphttp.default.input]
  }}
}}
";
    var response = new GetConfigResponse
    {
        Content = config
    };
    var responseBytes = response.ToByteArray();
    
    logger.LogInformation("GetConfig: ID={CollectorId}, User={Username}, ConfigSize={ConfigSize} bytes", 
        request.Id, username, responseBytes.Length);

    context.Response.Headers.Append("Connect-Protocol-Version", "1");
    context.Response.Headers.Append("Grpc-Status", "0");
    return Results.Bytes(responseBytes, "application/proto");
});

app.MapGet("/", () => "Remote Config Service Running");

app.Run();
