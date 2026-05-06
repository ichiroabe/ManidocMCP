using ManidocMCP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders(); // stdout をJSON-RPC専用に保つためログを無効化

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ManidocTools>();

await builder.Build().RunAsync();
