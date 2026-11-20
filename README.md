# Event Hub + Supplier System

**Full guide (what is done, how to run and test everything, how to explain it, what is left): [docs/PROJECT_GUIDE.md](docs/PROJECT_GUIDE.md)**

Two independent ASP.NET Core MVC apps. Each works on its own, with its own database. They only talk through RabbitMQ.

| App | Who uses it | What it does |
|---|---|---|
| **Event Hub** (`EventHub.Web`, port 5001) | Event organizers | Create events, list what each event needs (catering, decoration, sound), order from suppliers, follow order status |
| **Supplier System** (`SupplierSystem.Web`, port 5002) | Supplier staff | Several suppliers, each specialized in ONE category. Catalog, stock, incoming orders, status updates |

## How they talk
- Event Hub -> Supplier System: `SupplyOrderPlaced`, `OrderCancelled` (routing key ends with the category, e.g. `order.placed.catering`)
- Supplier System -> Event Hub: `CatalogPublished`, `ItemAvailabilityChanged`, `OrderStatusChanged`, `OrderRejected`, `InvoiceIssued`
- Event Hub -> Supplier System: also `PaymentCompleted` (organizer paid an invoice)
- Reliability: outbox table, publisher confirms, manual acks, retry queue + dead-letter queue, idempotent consumers (ProcessedMessages), MessageLog

## First run
```powershell
docker compose up -d                       # RabbitMQ + SQL Server
dotnet build
# migrations are in the repo and are applied on startup
# terminal 1
dotnet run --project SupplierSystem.Web --launch-profile http
# terminal 2 (optional: $env:Ai__ApiKey = "<gemini key>" first, for the AI suggestions)
dotnet run --project EventHub.Web --launch-profile http
```
Start the Supplier System first: it publishes the supplier catalogs when it seeds itself.

- RabbitMQ UI: http://localhost:15672 (eventhub / eventhub_pw)
- Event Hub: http://localhost:5001. Three kinds of user:
  - Organizer `organizer@eventhub.local` / `Organizer123!` (plans events, sells tickets, orders supplies)
  - Client `client@eventhub.local` / `Client123!` (browses events, buys tickets with a demo payment gateway)
  - Admin `admin@eventhub.local` / `Admin123!` (overview charts, Excel/PDF export, users, all events/orders/tickets, message monitor)
  - Anyone can also register as a client or organizer. The public Discover page needs no login.
- Supplier System: http://localhost:5002, password `Admin123!` for all:
  `admin@supplier.local` (platform admin, read-only), `manager.golden@supplier.local` and `manager.royal@supplier.local` (catering),
  `manager.bloom@supplier.local` (decoration), `manager.soundwave@supplier.local` (sound), `staff.golden@supplier.local` (processes orders only)

## Try the round trip
1. Event Hub: new event -> add needs -> "Find a supplier" -> order.
2. Supplier System: sign in as that category's manager -> Orders -> Accept -> Start preparing -> Dispatch -> Delivered.
3. Event Hub: the order status follows each step. Cancel an order or the whole event and the supplier sees it.

## Layout
- `EventHub.Contracts`: message records, categories, topology and routes (shared)
- `EventHub.Messaging`: connection, topology, publisher, outbox publisher, BaseConsumer
- `EventHub.Web` / `SupplierSystem.Web`: the two apps (models, repositories, services, controllers)
- `EventHub.Tests`: xUnit tests
