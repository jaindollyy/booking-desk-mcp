# Booking Desk MCP server in C#

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets an AI assistant look up flight bookings and request date changes, built with the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) and .NET 10.

Connect it to VS Code or any other MCP client, then ask things like:

> *"Can you check booking QX7P2L and move it two days later? The customer prefers a morning flight."*

The assistant calls `get_booking`, works out the new date and calls `request_date_change`. The request is created for a person to review. **The booking itself is never changed by the AI.**

All passenger data is fictional sample data.

## Tools

| Tool | What it does | Changes data? |
| --- | --- | --- |
| `get_booking` | Looks up a booking by its 6-character reference | No |
| `find_bookings_by_last_name` | Finds bookings when the customer doesn't know their reference | No |
| `request_date_change` | Creates a date change request for review | Creates a request only |
| `list_change_requests` | Lists requests waiting for review | No |

Read-only tools are marked as read-only in their MCP metadata, so clients can treat them as safe to call.

## Design decisions

- **Business rules live outside the MCP layer.** `BookingService` in `BookingDesk.Core` holds all the logic and has no MCP dependency, so it is unit tested on its own. `BookingTools` is a thin wrapper.
- **Every argument is validated.** The caller is an AI model, so references, dates and notes are checked before anything happens: reference format, date format, confirmed status only, no same-day or past dates, no more than 330 days ahead, no duplicate pending requests, and notes capped at 500 characters.
- **Human in the loop.** The only write action creates a request for review rather than editing the booking.
- **Clear results.** Every tool returns JSON with `success`, a plain-language `message` the model can relay, and the data.
- **Logs go to standard error.** MCP over stdio uses standard output for protocol messages, so logging is redirected to keep the channel clean.
- **Testable dates.** The service takes a `TimeProvider`, so date rules are tested against a fixed clock, and sample bookings are dated relative to today so the demo never goes stale.

## Project structure

| Project | What it contains |
| --- | --- |
| `src/BookingDesk.Core` | Models, in-memory repository with sample data, business rules |
| `src/BookingDesk.McpServer` | MCP server host and tool definitions |
| `tests/BookingDesk.Tests` | xUnit tests for the business rules |

## Run it

Requirements: .NET 10 SDK.

```bash
dotnet build
dotnet test
```

### Try it with MCP Inspector

```bash
npx @modelcontextprotocol/inspector dotnet run --project src/BookingDesk.McpServer
```

This opens a browser page where you can list the tools and call them by hand.

### Use it in VS Code

The repo includes `.vscode/mcp.json`. Open the folder in VS Code, start the `booking-desk` server from the MCP view, and use it from Copilot Chat in agent mode.

### Use it in another MCP client

Any MCP client that supports stdio servers can run it with this command:

```bash
dotnet run --project path/to/booking-desk-mcp/src/BookingDesk.McpServer
```

## Possible next steps

- Store bookings in SQL Server with Entity Framework Core
- Add an HTTP transport with authentication so the server can run remotely
- Add an approval tool for staff, kept on a separate server with stricter access
