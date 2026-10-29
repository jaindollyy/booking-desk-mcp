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
    public async Task A_booking_is_found_whatever_the_case_or_spacing(string reference)
    {
        var (service, _) = Create();

        var result = await service.GetBookingAsync(reference);

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
    public async Task An_invalid_reference_is_rejected_with_an_example(string reference)
    {
        var (service, _) = Create();

        var result = await service.GetBookingAsync(reference);

        Assert.False(result.Success);
        Assert.Contains("6 letters or numbers", result.Message);
    }

    [Fact]
    public async Task An_unknown_reference_says_it_was_not_found()
    {
        var (service, _) = Create();

        var result = await service.GetBookingAsync("AAAAAA");

        Assert.False(result.Success);
        Assert.Equal("No booking found with reference AAAAAA.", result.Message);
    }

    [Fact]
    public async Task Bookings_can_be_found_by_last_name_in_date_order()
    {
        var (service, _) = Create();

        var result = await service.FindByLastNameAsync("tan");

        Assert.True(result.Success);
        Assert.Equal(new[] { "ZT3W8N", "QX7P2L" }, result.Value!.Select(b => b.Reference));
    }

    [Fact]
    public async Task A_valid_date_change_creates_a_request_and_leaves_the_booking_unchanged()
    {
        var (service, repository) = Create();
        var newDate = Today.AddDays(23);

        var result = await service.RequestDateChangeAsync("QX7P2L", Iso(newDate), "Prefer a morning flight");

        Assert.True(result.Success);
        var request = result.Value!;
        Assert.Equal("CR-0001", request.Id);
        Assert.Equal(Today.AddDays(21), request.CurrentDate);
        Assert.Equal(newDate, request.RequestedDate);
        Assert.Equal("Prefer a morning flight", request.Note);
        Assert.Equal(ChangeRequestStatus.PendingReview, request.Status);
        Assert.Equal(Today.AddDays(21), (await repository.FindByReferenceAsync("QX7P2L"))!.TravelDate);
    }

    [Theory]
    [InlineData("14/12/2026")]
    [InlineData("next Tuesday")]
    [InlineData("")]
    public async Task A_date_in_the_wrong_format_is_rejected(string newDate)
    {
        var (service, _) = Create();

        var result = await service.RequestDateChangeAsync("QX7P2L", newDate);

        Assert.False(result.Success);
        Assert.Contains("YYYY-MM-DD", result.Message);
    }

    [Fact]
    public async Task Today_or_a_past_date_is_rejected()
    {
        var (service, _) = Create();

        Assert.False((await service.RequestDateChangeAsync("QX7P2L", Iso(Today))).Success);
        Assert.False((await service.RequestDateChangeAsync("QX7P2L", Iso(Today.AddDays(-1)))).Success);
    }

    [Fact]
    public async Task A_date_too_far_ahead_is_rejected()
    {
        var (service, _) = Create();

        var result = await service.RequestDateChangeAsync("QX7P2L", Iso(Today.AddDays(BookingService.MaxDaysAhead + 1)));

        Assert.False(result.Success);
        Assert.Contains("at most", result.Message);
    }

    [Fact]
    public async Task The_same_date_as_the_booking_is_rejected()
    {
        var (service, _) = Create();

        var result = await service.RequestDateChangeAsync("QX7P2L", Iso(Today.AddDays(21)));

        Assert.False(result.Success);
        Assert.Contains("already on", result.Message);
    }

    [Theory]
    [InlineData("LM2D5C", "cancelled")]
    [InlineData("RV6J1S", "flown")]
    public async Task Only_confirmed_bookings_can_be_changed(string reference, string status)
    {
        var (service, _) = Create();

        var result = await service.RequestDateChangeAsync(reference, Iso(Today.AddDays(40)));

        Assert.False(result.Success);
        Assert.Contains(status, result.Message);
    }

    [Fact]
    public async Task A_second_request_is_blocked_while_one_is_waiting_for_review()
    {
        var (service, _) = Create();
        Assert.True((await service.RequestDateChangeAsync("QX7P2L", Iso(Today.AddDays(25)))).Success);

        var second = await service.RequestDateChangeAsync("QX7P2L", Iso(Today.AddDays(26)));

        Assert.False(second.Success);
        Assert.Contains("waiting for review", second.Message);
    }

    [Fact]
    public async Task Change_requests_can_be_listed_all_by_booking_or_by_request_id()
    {
        var (service, _) = Create();
        await service.RequestDateChangeAsync("QX7P2L", Iso(Today.AddDays(25)));
        await service.RequestDateChangeAsync("HB4K9R", Iso(Today.AddDays(50)));

        var all = await service.ListChangeRequestsAsync();
        Assert.True(all.Success);
        Assert.Equal(2, all.Value!.Count);

        var byBooking = await service.ListChangeRequestsAsync("qx7p2l");
        Assert.Equal("QX7P2L", Assert.Single(byBooking.Value!).BookingReference);

        var byId = await service.ListChangeRequestsAsync("cr-0002");
        Assert.Equal("HB4K9R", Assert.Single(byId.Value!).BookingReference);
        Assert.Contains("CR-0002", byId.Message);
    }

    [Fact]
    public async Task An_unrecognised_filter_explains_what_to_enter()
    {
        var (service, _) = Create();

        var result = await service.ListChangeRequestsAsync("not-valid");

        Assert.False(result.Success);
        Assert.Contains("CR-0001", result.Message);
    }

    [Fact]
    public async Task An_empty_list_says_so()
    {
        var (service, _) = Create();

        var result = await service.ListChangeRequestsAsync();

        Assert.True(result.Success);
        Assert.Empty(result.Value!);
        Assert.Equal("No change requests found in total.", result.Message);
    }

    [Fact]
    public async Task Long_notes_are_trimmed_to_500_characters()
    {
        var (service, _) = Create();

        var result = await service.RequestDateChangeAsync("QX7P2L", Iso(Today.AddDays(25)), new string('x', 800));

        Assert.Equal(500, result.Value!.Note!.Length);
    }
}
