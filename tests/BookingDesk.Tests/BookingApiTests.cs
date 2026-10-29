using System.Net;
using BookingDesk.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BookingDesk.Tests;

/// <summary>
/// End-to-end tests of the path the MCP server uses: BookingService -> HttpBookingRepository -> Booking API.
/// The API runs in memory, so no port or network is needed.
/// </summary>
public class BookingApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    private HttpBookingRepository CreateRepository() => new(factory.CreateClient());

    [Fact]
    public async Task A_booking_is_read_from_the_api()
    {
        var booking = await CreateRepository().FindByReferenceAsync("QX7P2L");

        Assert.NotNull(booking);
        Assert.Equal("Tan", booking.PassengerLastName);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public async Task An_unknown_booking_comes_back_as_null()
    {
        Assert.Null(await CreateRepository().FindByReferenceAsync("AAAAAA"));
    }

    [Fact]
    public async Task Bookings_are_found_by_last_name_through_the_api()
    {
        var bookings = await CreateRepository().FindByLastNameAsync("tan");

        Assert.Equal(new[] { "ZT3W8N", "QX7P2L" }, bookings.Select(b => b.Reference));
    }

    [Fact]
    public async Task The_service_creates_a_change_request_through_the_api()
    {
        var service = new BookingService(CreateRepository(), TimeProvider.System);
        var newDate = Today.AddDays(46).ToString("yyyy-MM-dd");

        var result = await service.RequestDateChangeAsync("HB4K9R", newDate, "Window seat if possible");

        Assert.True(result.Success, result.Message);
        var listed = await service.ListChangeRequestsAsync("HB4K9R");
        Assert.Equal(result.Value!.Id, Assert.Single(listed.Value!).Id);
    }

    [Fact]
    public async Task The_api_rejects_a_change_request_for_an_unknown_booking()
    {
        var request = new NewChangeRequest("AAAAAA", Today, Today.AddDays(5), null, DateTimeOffset.UtcNow);

        var error = await Assert.ThrowsAsync<BookingApiException>(() => CreateRepository().AddChangeRequestAsync(request));

        Assert.Contains("404", error.Message);
    }

    [Fact]
    public async Task Searching_without_a_last_name_is_a_bad_request()
    {
        var response = await factory.CreateClient().GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
