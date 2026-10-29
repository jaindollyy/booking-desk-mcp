using System.Net;
using BookingDesk.Core;

namespace BookingDesk.Tests;

/// <summary>Returns a fixed response and records the request, so HTTP calls can be checked without a server.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        });
    }
}

public class HttpBookingRepositoryTests
{
    private static (HttpBookingRepository Repository, StubHandler Handler) Create(HttpStatusCode status, string body = "")
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://booking-api.test/") };
        return (new HttpBookingRepository(http), handler);
    }

    [Fact]
    public async Task A_booking_is_read_from_json()
    {
        var (repository, handler) = Create(HttpStatusCode.OK, """
            {"reference":"QX7P2L","passengerFirstName":"Mei","passengerLastName":"Tan","origin":"MEL",
             "destination":"SIN","travelDate":"2026-11-23","cabinClass":"Economy","status":"Confirmed"}
            """);

        var booking = await repository.FindByReferenceAsync("QX7P2L");

        Assert.Equal("http://booking-api.test/api/bookings/QX7P2L", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(new DateOnly(2026, 11, 23), booking!.TravelDate);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public async Task Not_found_returns_null()
    {
        var (repository, _) = Create(HttpStatusCode.NotFound);

        Assert.Null(await repository.FindByReferenceAsync("AAAAAA"));
    }

    [Fact]
    public async Task The_last_name_is_escaped_in_the_url()
    {
        var (repository, handler) = Create(HttpStatusCode.OK, "[]");

        await repository.FindByLastNameAsync("Van Der & Co");

        Assert.Equal("lastName=Van%20Der%20%26%20Co", handler.LastRequest!.RequestUri!.Query.TrimStart('?'));
    }

    [Fact]
    public async Task A_server_error_raises_a_booking_api_exception()
    {
        var (repository, _) = Create(HttpStatusCode.InternalServerError);

        var error = await Assert.ThrowsAsync<BookingApiException>(() => repository.GetChangeRequestsAsync());

        Assert.Contains("500", error.Message);
    }
}
