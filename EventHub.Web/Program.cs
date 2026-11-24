using EventHub.Web.Data;
using EventHub.Web.Messaging;
using EventHub.Web.Models;
using EventHub.Web.Repositories;
using EventHub.Web.Services;
using EventHub.Contracts;
using EventHub.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/eventhub-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();

// Database (this app's own SQL Server database)
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Identity: hashed passwords, password rules, lockout after repeated failures
builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Distinct cookie names: both apps run on localhost, and browsers share cookies across ports.
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "EventHub.Web.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddAntiforgery(o => o.Cookie.Name = "EventHub.Web.Antiforgery");

// Messaging: RabbitMQ, outbox, idempotency, message log, consumers
builder.Services.AddRabbitMessaging(builder.Configuration);
builder.Services.AddScoped<EfOutbox>();
builder.Services.AddScoped<IOutbox>(sp => sp.GetRequiredService<EfOutbox>());
builder.Services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<EfOutbox>());
builder.Services.AddScoped<IIdempotencyGuard, EfIdempotencyGuard>();
builder.Services.AddScoped<IMessageLog, EfMessageLog>();
builder.Services.AddOutboxPublisher();
builder.Services.AddConsumer<OrderUpdatesConsumer>();
builder.Services.AddConsumer<CatalogConsumer>();
builder.Services.AddConsumer<InvoicesConsumer>();

// Repositories and services
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IDoorService, DoorService>();
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

// Abuse limit: the public contact form accepts 5 messages a minute per IP address.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("contact", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Health checks: GET /health reports the database and the RabbitMQ connection.
builder.Services.AddHealthChecks()
    .AddCheck<EventHub.Web.Services.DatabaseHealthCheck>("database")
    .AddCheck<EventHub.Messaging.RabbitMqHealthCheck>("rabbitmq");
builder.Services.Configure<EventHub.Web.Services.Ai.AiOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.AddHttpClient<EventHub.Web.Services.Ai.IEventAdvisor, EventHub.Web.Services.Ai.EventAdvisor>(c => c.Timeout = TimeSpan.FromSeconds(60));   // the advisor enforces its own, shorter overall limit

var app = builder.Build();

await DbSeeder.MigrateAndSeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/status/{0}");
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHub<EventHub.Web.Hubs.LiveHub>("/hubs/live");
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { ResponseWriter = EventHub.Messaging.HealthReportWriter.WriteAsync });
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
