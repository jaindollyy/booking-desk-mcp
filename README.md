# Booking Desk MCP server in C#

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets an AI assistant look up flight bookings and request date changes, built with the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) and .NET 10.

Connect it to VS Code or any other MCP client, then ask things like:

> *"Can you check booking QX7P2L and move it two days later? The customer prefers a morning flight."*

The assistant calls `get_booking`, works out the new date and calls `request_date_change`. The request is created for a person to review. **The booking itself is never changed by the AI.**

All passenger data is fictional sample data.

## How the data flows

The MCP server doesn't own the booking data. It calls a Booking API over HTTP, the same way it would connect to a company's existing booking system.

```text
 AI assistant / MCP Inspector
            |
            |  MCP (JSON-RPC over stdio)
            v
 +---------------------------------------------+
 |  BookingDesk.McpServer                      |
 |                                             |
 |  BookingTools      tool definitions         |
 |       |                                     |
 |  BookingService    validation and rules     |
 |       |                                     |
 |  HttpBookingRepository   HttpClient         |
 +-------|-------------------------------------+
         |
         |  HTTP + JSON   GET  /api/bookings/{reference}
         |                GET  /api/bookings?lastName=
         |                GET  /api/change-requests
         |                POST /api/change-requests
         v
 +---------------------------------------------+
 |  BookingDesk.Api   (ASP.NET Core)           |
 |       |                                     |
 |  InMemoryBookingRepository   sample data    |
 +---------------------------------------------+
```

1. The assistant decides to call a tool, for example `get_booking` with `QX7P2L`, and sends the call over stdio.
2. `BookingTools` passes it to `BookingService`, which checks the input before anything else happens.
3. `HttpBookingRepository` calls `GET /api/bookings/QX7P2L` on the Booking API.
4. The API returns the booking as JSON, and the tool sends back `success`, a plain-language `message` and the data.

Both sides depend on the `IBookingRepository` interface, so the API can later be backed by SQL Server without changing the MCP server. If `BookingApi:BaseUrl` isn't set, the MCP server uses the in-memory sample data directly, so it still runs on its own.

## Tools

| Tool | What it does | Changes data? |
| --- | --- | --- |
| `get_booking` | Looks up a booking by its 6-character reference | No |
| `find_bookings_by_last_name` | Finds bookings when the customer doesn't know their reference | No |
| `request_date_change` | Creates a date change request for review | Creates a request only |
| `list_change_requests` | Lists requests waiting for review, filtered by booking reference or request ID | No |

Read-only tools are marked as read-only in their MCP metadata, so clients can treat them as safe to call.

## Design decisions

- **Business rules live outside the MCP layer.** `BookingService` in `BookingDesk.Core` holds all the logic and has no MCP dependency, so it is unit tested on its own. `BookingTools` is a thin wrapper.
- **Every argument is validated.** The caller is an AI model, so references, dates and notes are checked before anything happens: reference format, date format, confirmed status only, no same-day or past dates, no more than 330 days ahead, no duplicate pending requests, and notes capped at 500 characters.
- **Human in the loop.** The only write action creates a request for review rather than editing the booking.
- **Clear results.** Every tool returns JSON with `success`, a plain-language `message` the model can relay, and the data.
- **The API is the system of record.** The MCP server reads and writes through HTTP only. If the API is down, tools return a clear "booking system isn't available" message instead of an error trace.
- **Logs go to standard error.** MCP over stdio uses standard output for protocol messages, so logging is redirected to keep the channel clean.
- **Testable dates.** The service takes a `TimeProvider`, so date rules are tested against a fixed clock, and sample bookings are dated relative to today so the demo never goes stale.

## Project structure

| Project | What it contains |
| --- | --- |
| `src/BookingDesk.Core` | Models, business rules, in-memory and HTTP repositories |
| `src/BookingDesk.Api` | ASP.NET Core minimal API serving bookings and change requests |
| `src/BookingDesk.McpServer` | MCP server host and tool definitions |
| `tests/BookingDesk.Tests` | xUnit tests for the business rules, the HTTP client and the API end to end |

## Run it

Requirements: .NET 10 SDK.

```bash
dotnet build
dotnet test
```

### 1. Start the Booking API

```bash
dotnet run --project src/BookingDesk.Api
```

It runs on `http://localhost:5080`. Open `http://localhost:5080/api/bookings/QX7P2L` in a browser to check it, or use `src/BookingDesk.Api/BookingDesk.Api.http` in VS Code.

### 2. Try the MCP server with MCP Inspector

In a second terminal:

```bash
npx @modelcontextprotocol/inspector -e BookingApi__BaseUrl=http://localhost:5080 dotnet run --project src/BookingDesk.McpServer
```

This opens a browser page where you can list the tools and call them by hand. Each tool call shows up as an HTTP request in the API terminal.

To run the MCP server on its own with built-in sample data, leave out `-e BookingApi__BaseUrl=...`.

### Use it in VS Code

The repo includes `.vscode/mcp.json`. Open the folder in VS Code, start the Booking API first, then start the `booking-desk` server from the MCP view, and use it from Copilot Chat in agent mode.

### Use it in another MCP client

Any MCP client that supports stdio servers can run it with this command:

```bash
dotnet run --project path/to/booking-desk-mcp/src/BookingDesk.McpServer
```

## Possible next steps

- Store bookings in SQL Server with Entity Framework Core behind the API
- Add authentication between the MCP server and the API
- Add an HTTP transport with authentication so the server can run remotely
- Add an approval tool for staff, kept on a separate server with stricter access
