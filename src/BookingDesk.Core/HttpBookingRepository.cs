using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingDesk.Core;

/// <summary>
/// Reads and writes bookings through the Booking API over HTTP. This is how the MCP server
/// would connect to a company's existing booking system.
/// </summary>
public sealed class HttpBookingRepository(HttpClient http) : IBookingRepository
{
    /// <summary>JSON settings shared with the API: camelCase names and enums as text.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<Booking?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/bookings/{Uri.EscapeDataString(reference)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Booking>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> FindByLastNameAsync(string lastName, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/bookings?lastName={Uri.EscapeDataString(lastName)}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<Booking>>(JsonOptions, cancellationToken) ?? new List<Booking>();
    }

    public async Task<IReadOnlyList<ChangeRequest>> GetChangeRequestsAsync(string? bookingReference = null, CancellationToken cancellationToken = default)
    {
        var url = bookingReference is null
            ? "api/change-requests"
            : $"api/change-requests?bookingReference={Uri.EscapeDataString(bookingReference)}";

        using var response = await http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ChangeRequest>>(JsonOptions, cancellationToken) ?? new List<ChangeRequest>();
    }

    public async Task<ChangeRequest> AddChangeRequestAsync(NewChangeRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/change-requests", request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ChangeRequest>(JsonOptions, cancellationToken)
            ?? throw new BookingApiException("The Booking API returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new BookingApiException(
            $"The Booking API returned {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim());
    }
}

/// <summary>Raised when the Booking API can't be reached or returns an error.</summary>
public sealed class BookingApiException(string message, Exception? inner = null) : Exception(message, inner);
