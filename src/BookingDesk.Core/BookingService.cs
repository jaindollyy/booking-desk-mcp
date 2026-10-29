using System.Globalization;
using System.Text.RegularExpressions;

namespace BookingDesk.Core;

/// <summary>
/// Business rules for looking up bookings and requesting date changes. Every input is validated
/// here, because the caller is an AI model and its arguments can't be trusted blindly.
/// </summary>
public sealed partial class BookingService(IBookingRepository repository, TimeProvider timeProvider)
{
    /// <summary>How far ahead a new travel date can be.</summary>
    public const int MaxDaysAhead = 330;

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    public OperationResult<Booking> GetBooking(string reference)
    {
        if (!TryNormaliseReference(reference, out var normalised))
            return OperationResult<Booking>.Fail(InvalidReferenceMessage);

        var booking = repository.FindByReference(normalised);
        return booking is null
            ? OperationResult<Booking>.Fail($"No booking found with reference {normalised}.")
            : OperationResult<Booking>.Ok(booking, $"Found booking {normalised}.");
    }

    public OperationResult<IReadOnlyList<Booking>> FindByLastName(string lastName)
    {
        var name = lastName?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 50)
            return OperationResult<IReadOnlyList<Booking>>.Fail("Enter a last name between 2 and 50 characters.");

        var bookings = repository.FindByLastName(name);
        return bookings.Count == 0
            ? OperationResult<IReadOnlyList<Booking>>.Fail($"No bookings found for the last name {name}.")
            : OperationResult<IReadOnlyList<Booking>>.Ok(bookings, $"Found {bookings.Count} booking(s) for {name}.");
    }

    /// <summary>
    /// Creates a change request for a person to review. It never edits the booking itself.
    /// </summary>
    public OperationResult<ChangeRequest> RequestDateChange(string reference, string requestedDate, string? note = null)
    {
        if (!TryNormaliseReference(reference, out var normalised))
            return OperationResult<ChangeRequest>.Fail(InvalidReferenceMessage);

        if (!DateOnly.TryParseExact(requestedDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var newDate))
            return OperationResult<ChangeRequest>.Fail("Enter the new date in the format YYYY-MM-DD, for example 2026-12-14.");

        var booking = repository.FindByReference(normalised);
        if (booking is null)
            return OperationResult<ChangeRequest>.Fail($"No booking found with reference {normalised}.");

        if (booking.Status != BookingStatus.Confirmed)
            return OperationResult<ChangeRequest>.Fail(
                $"Booking {normalised} is {booking.Status.ToString().ToLowerInvariant()}, so its date can't be changed.");

        var today = Today;
        if (newDate <= today)
            return OperationResult<ChangeRequest>.Fail(
                "The new date must be after today. Same-day changes have to be made at the airport.");

        if (newDate > today.AddDays(MaxDaysAhead))
            return OperationResult<ChangeRequest>.Fail($"The new date can be at most {MaxDaysAhead} days from today.");

        if (newDate == booking.TravelDate)
            return OperationResult<ChangeRequest>.Fail($"Booking {normalised} is already on {Format(newDate)}.");

        if (repository.GetChangeRequests(normalised).Any(r => r.Status == ChangeRequestStatus.PendingReview))
            return OperationResult<ChangeRequest>.Fail(
                $"Booking {normalised} already has a change request waiting for review.");

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleanNote is { Length: > 500 })
            cleanNote = cleanNote[..500];

        var request = repository.AddChangeRequest(normalised, booking.TravelDate, newDate, cleanNote, timeProvider.GetUtcNow());
        return OperationResult<ChangeRequest>.Ok(
            request,
            $"Created change request {request.Id} to move {normalised} from {Format(booking.TravelDate)} to {Format(newDate)}. " +
            "A team member will review it; the booking stays as it is until then.");
    }

    public IReadOnlyList<ChangeRequest> ListChangeRequests(string? reference = null)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return repository.GetChangeRequests();

        return TryNormaliseReference(reference, out var normalised)
            ? repository.GetChangeRequests(normalised)
            : [];
    }

    private const string InvalidReferenceMessage =
        "A booking reference is 6 letters or numbers, for example QX7P2L.";

    internal static bool TryNormaliseReference(string? reference, out string normalised)
    {
        normalised = reference?.Trim().ToUpperInvariant() ?? string.Empty;
        return ReferencePattern().IsMatch(normalised);
    }

    private static string Format(DateOnly date) => date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    [GeneratedRegex("^[A-Z0-9]{6}$")]
    private static partial Regex ReferencePattern();
}
