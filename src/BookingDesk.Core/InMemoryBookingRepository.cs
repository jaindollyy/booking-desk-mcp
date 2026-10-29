namespace BookingDesk.Core;

public interface IBookingRepository
{
    Booking? FindByReference(string reference);

    IReadOnlyList<Booking> FindByLastName(string lastName);

    IReadOnlyList<ChangeRequest> GetChangeRequests(string? bookingReference = null);

    ChangeRequest AddChangeRequest(string bookingReference, DateOnly currentDate, DateOnly requestedDate, string? note, DateTimeOffset createdAt);
}

/// <summary>
/// Thread-safe in-memory storage with fictional sample data. Swap it for a database-backed
/// implementation of <see cref="IBookingRepository"/> in a real system.
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

    public Booking? FindByReference(string reference)
    {
        lock (_gate)
            return _bookings.GetValueOrDefault(reference);
    }

    public IReadOnlyList<Booking> FindByLastName(string lastName)
    {
        lock (_gate)
        {
            return _bookings.Values
                .Where(b => string.Equals(b.PassengerLastName, lastName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.TravelDate)
                .ToList();
        }
    }

    public IReadOnlyList<ChangeRequest> GetChangeRequests(string? bookingReference = null)
    {
        lock (_gate)
        {
            return _changeRequests
                .Where(r => bookingReference is null
                    || string.Equals(r.BookingReference, bookingReference, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public ChangeRequest AddChangeRequest(string bookingReference, DateOnly currentDate, DateOnly requestedDate, string? note, DateTimeOffset createdAt)
    {
        lock (_gate)
        {
            var request = new ChangeRequest(
                $"CR-{_changeRequests.Count + 1:D4}",
                bookingReference,
                currentDate,
                requestedDate,
                note,
                createdAt,
                ChangeRequestStatus.PendingReview);

            _changeRequests.Add(request);
            return request;
        }
    }
}
