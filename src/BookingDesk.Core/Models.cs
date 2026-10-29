namespace BookingDesk.Core;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    Flown,
}

public enum ChangeRequestStatus
{
    PendingReview,
}

public sealed record Booking(
    string Reference,
    string PassengerFirstName,
    string PassengerLastName,
    string Origin,
    string Destination,
    DateOnly TravelDate,
    string CabinClass,
    BookingStatus Status);

/// <summary>
/// A request to move a booking to a new date. It is created for a person to review;
/// the booking itself is never changed directly by the AI assistant.
/// </summary>
public sealed record ChangeRequest(
    string Id,
    string BookingReference,
    DateOnly CurrentDate,
    DateOnly RequestedDate,
    string? Note,
    DateTimeOffset CreatedAt,
    ChangeRequestStatus Status);

/// <summary>The outcome of an operation, with a message written for the person who asked.</summary>
public sealed record OperationResult<T>(bool Success, string Message, T? Value)
{
    public static OperationResult<T> Ok(T value, string message) => new(true, message, value);

    public static OperationResult<T> Fail(string message) => new(false, message, default);
}
