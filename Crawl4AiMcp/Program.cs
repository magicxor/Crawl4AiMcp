using System.Net.Http.Headers;
using Crawl4AiMcp.Client;
using Crawl4AiMcp.Configuration;
using Crawl4AiMcp.IO;
using Crawl4AiMcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Bind the crawl4ai instance configuration ("Crawl4Ai" section of appsettings.json
// or the Crawl4Ai__* environment variables).
builder.Services.Configure<Crawl4AiOptions>(builder.Configuration.GetSection(Crawl4AiOptions.SectionName));

// Typed HttpClient pointed at the configured crawl4ai REST instance.
builder.Services.AddHttpClient<Crawl4AiClient>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<Crawl4AiOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(options.ApiToken))
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
});

builder.Services.AddSingleton<ArtifactWriter>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<Crawl4AiTools>();

await builder.Build().RunAsync();
