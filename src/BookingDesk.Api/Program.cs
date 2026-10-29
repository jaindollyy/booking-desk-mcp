using System.Text.Json.Serialization;
using BookingDesk.Core;

// A small booking API standing in for a company's existing booking system.
// The MCP server calls it over HTTP instead of touching the data directly.
var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBookingRepository>(services =>
{
    var now = services.GetRequiredService<TimeProvider>().GetLocalNow();
    return InMemoryBookingRepository.CreateWithSampleData(DateOnly.FromDateTime(now.DateTime));
});

var app = builder.Build();

app.MapGet("/", () => "Booking API is running. Try GET /api/bookings/QX7P2L");

var api = app.MapGroup("/api");

api.MapGet("/bookings/{reference}", async (string reference, IBookingRepository repository, CancellationToken ct) =>
    await repository.FindByReferenceAsync(reference, ct) is { } booking
        ? Results.Ok(booking)
        : Results.NotFound());

api.MapGet("/bookings", async (string? lastName, IBookingRepository repository, CancellationToken ct) =>
    string.IsNullOrWhiteSpace(lastName)
        ? Results.BadRequest("Add a lastName query parameter, for example /api/bookings?lastName=Tan")
        : Results.Ok(await repository.FindByLastNameAsync(lastName, ct)));

api.MapGet("/change-requests", async (string? bookingReference, IBookingRepository repository, CancellationToken ct) =>
    Results.Ok(await repository.GetChangeRequestsAsync(bookingReference, ct)));

api.MapPost("/change-requests", async (NewChangeRequest request, IBookingRepository repository, CancellationToken ct) =>
{
    if (await repository.FindByReferenceAsync(request.BookingReference, ct) is null)
        return Results.NotFound($"No booking found with reference {request.BookingReference}.");

    var created = await repository.AddChangeRequestAsync(request, ct);
    return Results.Created($"/api/change-requests?bookingReference={created.BookingReference}", created);
});

app.Run();

// Lets the integration tests start the API in memory.
public partial class Program;
