# Event Hub

Two independent ASP.NET Core MVC apps (Client portal, Admin control center) linked only by RabbitMQ.

## Quick start
```powershell
powershell -ExecutionPolicy Bypass -File .\setup.ps1   # once: creates apps, adds packages, starts Docker, builds
```
- RabbitMQ UI: http://localhost:15672 (eventhub / eventhub_pw)
- Client: http://localhost:5001  ·  Admin: http://localhost:5002

## Layout
- `EventHub.Contracts`: message records, topology constants, routes (shared by both apps)
- `EventHub.Messaging`: connection, topology, publisher (confirms), BaseConsumer (ack/retry/DLQ/idempotency)
- `EventHub.Client` / `EventHub.Admin`: the two MVC apps (each with its own SQL Server database)
- `EventHub.Tests`: xUnit tests
