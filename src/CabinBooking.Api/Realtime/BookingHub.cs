using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CabinBooking.Api.Realtime;

/// <summary>
/// Real-time channel. Clients only listen; all changes go through the REST API and are broadcast from here.
/// Events: BookingCreated(BookingDto), BookingUpdated(BookingDto), BookingCancelled(BookingDto), CabinsChanged().
/// </summary>
[Authorize]
public class BookingHub : Hub
{
    public const string Path = "/hubs/bookings";
}

public class SignalRBookingNotifier(IHubContext<BookingHub> hub, ILogger<SignalRBookingNotifier> logger) : IBookingNotifier
{
    public Task BookingCreatedAsync(BookingDto booking) => Send("BookingCreated", booking);
    public Task BookingUpdatedAsync(BookingDto booking) => Send("BookingUpdated", booking);
    public Task BookingCancelledAsync(BookingDto booking) => Send("BookingCancelled", booking);
    public Task CabinsChangedAsync() => Send("CabinsChanged", null);

    private async Task Send(string method, object? payload)
    {
        try
        {
            if (payload is null) await hub.Clients.All.SendAsync(method);
            else await hub.Clients.All.SendAsync(method, payload);
        }
        catch (Exception ex)
        {
            // The change is already saved; a failed broadcast must not fail the request.
            // Clients resync on reconnect.
            logger.LogWarning(ex, "Failed to broadcast {Method}", method);
        }
    }
}
