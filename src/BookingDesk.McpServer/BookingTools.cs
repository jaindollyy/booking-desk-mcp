using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookingDesk.Core;
using ModelContextProtocol.Server;

namespace BookingDesk.McpServer;

/// <summary>
/// The tools this MCP server exposes. Each one is a thin wrapper over <see cref="BookingService"/>,
/// which holds the business rules. Results are returned as JSON so the model can read them reliably.
/// </summary>
[McpServerToolType]
public sealed class BookingTools(BookingService bookings)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [McpServerTool(Name = "get_booking", ReadOnly = true)]
    [Description("Look up a flight booking by its 6-character booking reference. Returns the passenger, route, travel date, cabin class and status.")]
    public string GetBooking(
        [Description("The 6-character booking reference, for example QX7P2L.")] string reference)
        => ToJson(bookings.GetBooking(reference));

    [McpServerTool(Name = "find_bookings_by_last_name", ReadOnly = true)]
    [Description("Find all bookings for a passenger's last name. Use this when the customer doesn't know their booking reference.")]
    public string FindBookingsByLastName(
        [Description("The passenger's last name, for example Tan.")] string lastName)
        => ToJson(bookings.FindByLastName(lastName));

    [McpServerTool(Name = "request_date_change")]
    [Description("Create a request to move a confirmed booking to a new travel date. The request is reviewed by a person before anything changes; the booking itself is not modified.")]
    public string RequestDateChange(
        [Description("The 6-character booking reference.")] string reference,
        [Description("The new travel date in YYYY-MM-DD format.")] string newDate,
        [Description("Optional note from the customer, such as a reason or a preferred time.")] string? note = null)
        => ToJson(bookings.RequestDateChange(reference, newDate, note));

    [McpServerTool(Name = "list_change_requests", ReadOnly = true)]
    [Description("List change requests waiting for review, optionally for one booking reference.")]
    public string ListChangeRequests(
        [Description("Optional 6-character booking reference to filter by.")] string? reference = null)
    {
        var requests = bookings.ListChangeRequests(reference);
        return JsonSerializer.Serialize(new { count = requests.Count, requests }, JsonOptions);
    }

    private static string ToJson<T>(OperationResult<T> result) =>
        JsonSerializer.Serialize(new { success = result.Success, message = result.Message, data = result.Value }, JsonOptions);
}
