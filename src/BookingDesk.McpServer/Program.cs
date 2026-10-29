using BookingDesk.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// MCP over stdio uses standard output for protocol messages, so every log line must go to standard error.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBookingRepository>(services =>
{
    var now = services.GetRequiredService<TimeProvider>().GetLocalNow();
    return InMemoryBookingRepository.CreateWithSampleData(DateOnly.FromDateTime(now.DateTime));
});
builder.Services.AddSingleton<BookingService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
