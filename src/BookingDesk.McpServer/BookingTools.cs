using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookingDesk.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookingDesk.McpServer;

/// <summary>
/// The tools this MCP server exposes. Each one is a thin wrapper over <see cref="BookingService"/>,
/// which holds the business rules. Results are returned as JSON so the model can read them reliably.
/// </summary>
[McpServerToolType]
public sealed class BookingTools(BookingService bookings, ILogger<BookingTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [McpServerTool(Name = "get_booking", ReadOnly = true)]
    [Description("Look up a flight booking by its 6-character booking reference. Returns the passenger, route, travel date, cabin class and status.")]
    public Task<string> GetBooking(
        [Description("The 6-character booking reference, for example QX7P2L.")] string reference,
        CancellationToken cancellationToken)
        => RunAsync(() => bookings.GetBookingAsync(reference, cancellationToken));

    [McpServerTool(Name = "find_bookings_by_last_name", ReadOnly = true)]
    [Description("Find all bookings for a passenger's last name. Use this when the customer doesn't know their booking reference.")]
    public Task<string> FindBookingsByLastName(
        [Description("The passenger's last name, for example Tan.")] string lastName,
        CancellationToken cancellationToken)
        => RunAsync(() => bookings.FindByLastNameAsync(lastName, cancellationToken));

    [McpServerTool(Name = "request_date_change")]
    [Description("Create a request to move a confirmed booking to a new travel date. The request is reviewed by a person before anything changes; the booking itself is not modified.")]
    public Task<string> RequestDateChange(
        [Description("The 6-character booking reference.")] string reference,
        [Description("The new travel date in YYYY-MM-DD format.")] string newDate,
        [Description("Optional note from the customer, such as a reason or a preferred time.")] string? note = null,
        CancellationToken cancellationToken = default)
        => RunAsync(() => bookings.RequestDateChangeAsync(reference, newDate, note, cancellationToken));

    [McpServerTool(Name = "list_change_requests", ReadOnly = true)]
    [Description("List change requests waiting for review. Leave the filter empty for all requests, or filter by a booking reference or a change request ID.")]
    public Task<string> ListChangeRequests(
        [Description("Optional filter: a booking reference such as QX7P2L, or a change request ID such as CR-0001.")] string? filter = null,
        CancellationToken cancellationToken = default)
        => RunAsync(() => bookings.ListChangeRequestsAsync(filter, cancellationToken));

    /// <summary>
    /// Runs a service call and turns it into JSON. If the Booking API is down, the model gets a clear
    /// message to pass on instead of a stack trace.
    /// </summary>
    private async Task<string> RunAsync<T>(Func<Task<OperationResult<T>>> call)
    {
        try
        {
            return ToJson(await call());
        }
        catch (Exception ex) when (ex is BookingApiException or HttpRequestException)
        {
            logger.LogError(ex, "Booking API call failed");
            return ToJson(OperationResult<T>.Fail(
                "The booking system isn't available right now. Please try again in a few minutes."));
        }
    }

    private static string ToJson<T>(OperationResult<T> result) =>
        JsonSerializer.Serialize(new { success = result.Success, message = result.Message, data = result.Value }, JsonOptions);
}
