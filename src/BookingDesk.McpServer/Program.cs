using BookingDesk.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// MCP over stdio uses standard output for protocol messages, so every log line must go to standard error.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(TimeProvider.System);

// Where the booking data comes from:
// - BookingApi:BaseUrl set (env var BookingApi__BaseUrl) -> fetch it from the Booking API over HTTP
// - not set                                              -> use built-in sample data, so the server runs on its own
var apiBaseUrl = builder.Configuration["BookingApi:BaseUrl"];
if (!string.IsNullOrWhiteSpace(apiBaseUrl))
{
    builder.Services.AddHttpClient<IBookingRepository, HttpBookingRepository>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
}
else
{
    builder.Services.AddSingleton<IBookingRepository>(services =>
    {
        var now = services.GetRequiredService<TimeProvider>().GetLocalNow();
        return InMemoryBookingRepository.CreateWithSampleData(DateOnly.FromDateTime(now.DateTime));
    });
}

builder.Services.AddTransient<BookingService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
