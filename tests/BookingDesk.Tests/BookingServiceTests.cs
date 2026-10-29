using BookingDesk.Core;

namespace BookingDesk.Tests;

/// <summary>A clock fixed at a known moment, so date rules can be tested.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

public class BookingServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);

    private static (BookingService Service, InMemoryBookingRepository Repository) Create()
    {
        var repository = InMemoryBookingRepository.CreateWithSampleData(Today);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
        return (new BookingService(repository, clock), repository);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    [Theory]
    [InlineData("QX7P2L")]
    [InlineData("qx7p2l")]
    [InlineData("  QX7P2L ")]
    public void A_booking_is_found_whatever_the_case_or_spacing(string reference)
    {
        var (service, _) = Create();

        var result = service.GetBooking(reference);

        Assert.True(result.Success);
        Assert.Equal("QX7P2L", result.Value!.Reference);
        Assert.Equal("SIN", result.Value.Destination);
    }

    [Theory]
    [InlineData("")]
    [InlineData("QX7")]
    [InlineData("QX7P2L9")]
    [InlineData("QX7-2L")]
    [InlineData("DROP TABLE")]
    public void An_invalid_reference_is_rejected_with_an_example(string reference)
    {
        var (service, _) = Create();

        var result = service.GetBooking(reference);

        Assert.False(result.Success);
        Assert.Contains("6 letters or numbers", result.Message);
    }

    [Fact]
    public void An_unknown_reference_says_it_was_not_found()
    {
        var (service, _) = Create();

        var result = service.GetBooking("AAAAAA");

        Assert.False(result.Success);
        Assert.Equal("No booking found with reference AAAAAA.", result.Message);
    }

    [Fact]
    public void Bookings_can_be_found_by_last_name_in_date_order()
    {
        var (service, _) = Create();

        var result = service.FindByLastName("tan");

        Assert.True(result.Success);
        Assert.Equal(new[] { "ZT3W8N", "QX7P2L" }, result.Value!.Select(b => b.Reference));
    }

    [Fact]
    public void A_valid_date_change_creates_a_request_and_leaves_the_booking_unchanged()
    {
        var (service, repository) = Create();
        var newDate = Today.AddDays(23);

        var result = service.RequestDateChange("QX7P2L", Iso(newDate), "Prefer a morning flight");

        Assert.True(result.Success);
        var request = result.Value!;
        Assert.Equal("CR-0001", request.Id);
        Assert.Equal(Today.AddDays(21), request.CurrentDate);
        Assert.Equal(newDate, request.RequestedDate);
        Assert.Equal("Prefer a morning flight", request.Note);
        Assert.Equal(ChangeRequestStatus.PendingReview, request.Status);
        Assert.Equal(Today.AddDays(21), repository.FindByReference("QX7P2L")!.TravelDate);
    }

    [Theory]
    [InlineData("14/12/2026")]
    [InlineData("next Tuesday")]
    [InlineData("")]
    public void A_date_in_the_wrong_format_is_rejected(string newDate)
    {
        var (service, _) = Create();

        var result = service.RequestDateChange("QX7P2L", newDate);

        Assert.False(result.Success);
        Assert.Contains("YYYY-MM-DD", result.Message);
    }

    [Fact]
    public void Today_or_a_past_date_is_rejected()
    {
        var (service, _) = Create();

        Assert.False(service.RequestDateChange("QX7P2L", Iso(Today)).Success);
        Assert.False(service.RequestDateChange("QX7P2L", Iso(Today.AddDays(-1))).Success);
    }

    [Fact]
    public void A_date_too_far_ahead_is_rejected()
    {
        var (service, _) = Create();

        var result = service.RequestDateChange("QX7P2L", Iso(Today.AddDays(BookingService.MaxDaysAhead + 1)));

        Assert.False(result.Success);
        Assert.Contains("at most", result.Message);
    }

    [Fact]
    public void The_same_date_as_the_booking_is_rejected()
    {
        var (service, _) = Create();

        var result = service.RequestDateChange("QX7P2L", Iso(Today.AddDays(21)));

        Assert.False(result.Success);
        Assert.Contains("already on", result.Message);
    }

    [Theory]
    [InlineData("LM2D5C", "cancelled")]
    [InlineData("RV6J1S", "flown")]
    public void Only_confirmed_bookings_can_be_changed(string reference, string status)
    {
        var (service, _) = Create();

        var result = service.RequestDateChange(reference, Iso(Today.AddDays(40)));

        Assert.False(result.Success);
        Assert.Contains(status, result.Message);
    }

    [Fact]
    public void A_second_request_is_blocked_while_one_is_waiting_for_review()
    {
        var (service, _) = Create();
        Assert.True(service.RequestDateChange("QX7P2L", Iso(Today.AddDays(25))).Success);

        var second = service.RequestDateChange("QX7P2L", Iso(Today.AddDays(26)));

        Assert.False(second.Success);
        Assert.Contains("waiting for review", second.Message);
    }

    [Fact]
    public void Change_requests_can_be_listed_for_one_booking_or_all()
    {
        var (service, _) = Create();
        service.RequestDateChange("QX7P2L", Iso(Today.AddDays(25)));
        service.RequestDateChange("HB4K9R", Iso(Today.AddDays(50)));

        Assert.Equal(2, service.ListChangeRequests().Count);
        Assert.Equal("QX7P2L", Assert.Single(service.ListChangeRequests("qx7p2l")).BookingReference);
        Assert.Empty(service.ListChangeRequests("not-valid"));
    }

    [Fact]
    public void Long_notes_are_trimmed_to_500_characters()
    {
        var (service, _) = Create();

        var result = service.RequestDateChange("QX7P2L", Iso(Today.AddDays(25)), new string('x', 800));

        Assert.Equal(500, result.Value!.Note!.Length);
    }
}
