using SupplierSystem.Web.Data;
using SupplierSystem.Web.Messaging;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Repositories;
using SupplierSystem.Web.Services;
using EventHub.Contracts;
using EventHub.Messaging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/supplier-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<StaffUser, IdentityRole<Guid>>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<StaffClaimsFactory>()
    .AddDefaultTokenProviders();

// Distinct cookie names: both apps run on localhost, and browsers share cookies across ports.
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "SupplierSystem.Web.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddAntiforgery(o => o.Cookie.Name = "SupplierSystem.Web.Antiforgery");

// Role-based policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanProcessOrders", p => p.RequireRole(Roles.SupplierManager, Roles.SupplierStaff))
    .AddPolicy("CanManageCatalog", p => p.RequireRole(Roles.SupplierManager));

// Messaging: RabbitMQ, outbox, idempotency, message log, consumers
builder.Services.AddRabbitMessaging(builder.Configuration);
builder.Services.AddScoped<EfOutbox>();
builder.Services.AddScoped<IOutbox>(sp => sp.GetRequiredService<EfOutbox>());
builder.Services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<EfOutbox>());
builder.Services.AddScoped<IIdempotencyGuard, EfIdempotencyGuard>();
builder.Services.AddScoped<IMessageLog, EfMessageLog>();
builder.Services.AddOutboxPublisher();

// One consumer per category queue (supplier.orders.catering, .decoration, .sound), each its own background service.
foreach (var category in SupplyCategories.All)
{
    var queue = Topology.Queues.SupplierOrders(category);
    builder.Services.AddSingleton<IHostedService>(sp => new OrdersConsumer(
        queue,
        sp.GetRequiredService<IRabbitMqConnection>(),
        sp.GetRequiredService<TopologyDeclarer>(),
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<IOptions<RabbitMqOptions>>(),
        sp.GetRequiredService<ILogger<OrdersConsumer>>()));
}

builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddConsumer<PaymentsConsumer>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ITeamService, TeamService>();

// Health checks: GET /health reports the database and the RabbitMQ connection.
builder.Services.AddHealthChecks()
    .AddCheck<SupplierSystem.Web.Services.DatabaseHealthCheck>("database")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");

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
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHub<SupplierSystem.Web.Hubs.LiveHub>("/hubs/live");
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { ResponseWriter = HealthReportWriter.WriteAsync });
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Orders}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
