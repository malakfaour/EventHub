# Progress

## Done
- [x] Solution, projects, references (`setup.ps1`, SDK = .NET 9)
- [x] Docker Compose: RabbitMQ + SQL Server
- [x] `EventHub.Contracts`: 15 messages, topology constants, routes, serializer
- [x] `EventHub.Messaging`: connection, topology, publisher (confirms), `BaseConsumer<T>` (ack, retry, DLQ, idempotency)

## Next (Phase 1 -> 2)
- [ ] DbContexts + Identity + seed data in Client and Admin
- [ ] `OutboxMessages`, `ProcessedMessages`, `MessageLog` tables (both apps)
- [ ] `IIdempotencyGuard` + `IMessageLog` implementations (EF Core)
- [ ] `OutboxPublisher` background service
- [ ] First round trip: EventSubmitted -> approve/reject -> EventApproved/EventRejected

## How to work
1. `git pull`, `docker compose up -d`, `dotnet build EventHub.sln`
2. One feature per branch, small commits, pull before you push.
3. Ask Claude to read this file and continue from "Next".
