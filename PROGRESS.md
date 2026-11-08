# Progress

## Done
- [x] Solution, projects, references (`setup.ps1`, SDK = .NET 9)
- [x] Docker Compose: RabbitMQ + SQL Server
- [x] `EventHub.Contracts`: 15 messages, topology, routes, serializer, seed categories
- [x] `EventHub.Messaging`: connection, topology, publisher (confirms), `BaseConsumer` (ack, retry, DLQ, idempotency, multi-type queues), `OutboxPublisher`
- [x] Both apps: DbContext + Identity, outbox, `ProcessedMessages` idempotency, `MessageLog` table, repositories, services
- [x] Round trip code: Client creates event (Pending) -> `EventSubmitted` -> Admin queue -> approve/reject -> `EventApproved/Rejected` -> Client status
- [ ] NOT YET RUN: first migrations + end-to-end test (see below)

## First run (do once, then commit the Migrations folders)
```powershell
dotnet build EventHub.sln
dotnet ef migrations add Initial --project EventHub.Client --output-dir Data/Migrations
dotnet ef migrations add Initial --project EventHub.Admin  --output-dir Data/Migrations
docker compose up -d
dotnet run --project EventHub.Admin     # http://localhost:5002
dotnet run --project EventHub.Client    # http://localhost:5001
```
Demo logins: member@eventhub.local / Member123!  |  moderator@eventhub.local, admin@eventhub.local / Admin123!

## Next
- [ ] Message log page + dead-letter viewer/replay (Admin)
- [ ] SignalR: live moderation queue (Admin), live status toast (Client)
- [ ] UserRegistered publish + Members directory; EventUpdated/Cancelled/Reported
- [ ] RSVP, comments, ratings, reports; remaining messages
- [ ] AI pre-screen + writing assistant; payments; dashboard; Docker for the apps; tests; docs

## How to work
1. `git pull`, `docker compose up -d`, `dotnet build EventHub.sln`
2. One feature per branch, small commits, pull before you push.
3. Ask Claude to read this file and continue from "Next".
