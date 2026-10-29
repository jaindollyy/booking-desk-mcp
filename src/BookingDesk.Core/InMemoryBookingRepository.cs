namespace BookingDesk.Core;

/// <summary>
/// Where bookings and change requests are stored. The MCP server uses either the in-memory version
/// (for a self-contained demo) or <see cref="HttpBookingRepository"/>, which calls the Booking API.
/// </summary>
public interface IBookingRepository
{
    Task<Booking?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> FindByLastNameAsync(string lastName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChangeRequest>> GetChangeRequestsAsync(string? bookingReference = null, CancellationToken cancellationToken = default);

    Task<ChangeRequest> AddChangeRequestAsync(NewChangeRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The data needed to create a change request.</summary>
public sealed record NewChangeRequest(
    string BookingReference,
    DateOnly CurrentDate,
    DateOnly RequestedDate,
    string? Note,
    DateTimeOffset CreatedAt);

/// <summary>
/// Thread-safe in-memory storage with fictional sample data. The Booking API uses it as its data store,
/// and the MCP server can use it directly when no API is configured.
/// </summary>
public sealed class InMemoryBookingRepository : IBookingRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Booking> _bookings;
    private readonly List<ChangeRequest> _changeRequests = [];

    public InMemoryBookingRepository(IEnumerable<Booking> bookings)
    {
        _bookings = bookings.ToDictionary(b => b.Reference, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Creates a repository with sample bookings dated relative to today, so the demo never goes stale.</summary>
    public static InMemoryBookingRepository CreateWithSampleData(DateOnly today) => new(
    [
        new("QX7P2L", "Mei", "Tan", "MEL", "SIN", today.AddDays(21), "Economy", BookingStatus.Confirmed),
        new("HB4K9R", "Arjun", "Mehta", "SYD", "LHR", today.AddDays(45), "Business", BookingStatus.Confirmed),
        new("ZT3W8N", "Sarah", "Tan", "BNE", "AKL", today.AddDays(7), "Economy", BookingStatus.Confirmed),
        new("LM2D5C", "Tom", "Okafor", "PER", "DPS", today.AddDays(30), "Economy", BookingStatus.Cancelled),
        new("RV6J1S", "Lena", "Novak", "ADL", "MEL", today.AddDays(-10), "Economy", BookingStatus.Flown),
    ]);

    public Task<Booking?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(_bookings.GetValueOrDefault(reference));
    }

    public Task<IReadOnlyList<Booking>> FindByLastNameAsync(string lastName, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Booking> matches = _bookings.Values
                .Where(b => string.Equals(b.PassengerLastName, lastName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.TravelDate)
                .ToList();
            return Task.FromResult(matches);
        }
    }

    public Task<IReadOnlyList<ChangeRequest>> GetChangeRequestsAsync(string? bookingReference = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ChangeRequest> matches = _changeRequests
                .Where(r => bookingReference is null
                    || string.Equals(r.BookingReference, bookingReference, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult(matches);
        }
    }

    public Task<ChangeRequest> AddChangeRequestAsync(NewChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var created = new ChangeRequest(
                $"CR-{_changeRequests.Count + 1:D4}",
                request.BookingReference.ToUpperInvariant(),
                request.CurrentDate,
                request.RequestedDate,
                request.Note,
                request.CreatedAt,
                ChangeRequestStatus.PendingReview);

            _changeRequests.Add(created);
            return Task.FromResult(created);
        }
    }
}
