# ADR 0003: SignalR Realtime Architecture for Multi-Client Synchronization

## Status
Accepted

## Context
When a user books, checks into, cancels, or extends a room, all other users currently viewing the live room board or timeline should see the room status update immediately without polling or refreshing the page.

## Decision
We adopted **ASP.NET Core SignalR** with WebSockets as primary transport (falling back to Server-Sent Events / Long Polling):
1. **Real-time Hub:** `BookingHub` (`/hubs/bookings`) coordinates connected clients.
2. **Decoupled Notifier:** Application services broadcast mutations via the `IBookingNotifier` abstraction, implemented by `SignalRBookingNotifier` in `CabinBooking.Api`.
3. **Payload Efficiency:** Broadcast events transmit updated entity snapshots (`BookingCreated`, `BookingUpdated`, `BookingCancelled`, `CabinStatusChanged`), enabling clients to update their React state or TanStack Query cache locally.
4. **WebSocket Authentication:** Browsers do not support custom HTTP headers on WebSocket handshakes. The JWT is transmitted in the query string (`?access_token=...`) during handshake and validated via `JwtBearerEvents.OnMessageReceived`.

## Consequences
### Positive
- Sub-second UI updates across distributed client browsers.
- Dramatically reduced server CPU and database load compared to client polling.
- Resilient connection management with automatic reconnect in `@microsoft/signalr`.

### Negative
- Requires stateful WebSocket connections on the web server (or a backplane like Redis / Azure SignalR Service when scaling out horizontally across multiple instances).
