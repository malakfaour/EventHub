using EventHub.Client.Data;
using EventHub.Client.Messaging;
using EventHub.Client.Models;
using EventHub.Client.Repositories;
using EventHub.Client.Services;
using EventHub.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/client-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddControllersWithViews();

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
    o.Cookie.Name = "EventHub.Client.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddAntiforgery(o => o.Cookie.Name = "EventHub.Client.Antiforgery");

// Messaging: RabbitMQ, outbox, idempotency, message log, consumers
builder.Services.AddRabbitMessaging(builder.Configuration);
builder.Services.AddScoped<EfOutbox>();
builder.Services.AddScoped<IOutbox>(sp => sp.GetRequiredService<EfOutbox>());
builder.Services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<EfOutbox>());
builder.Services.AddScoped<IIdempotencyGuard, EfIdempotencyGuard>();
builder.Services.AddScoped<IMessageLog, EfMessageLog>();
builder.Services.AddOutboxPublisher();
builder.Services.AddConsumer<ModerationResultsConsumer>();

// Repositories and services
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IEventService, EventService>();

var app = builder.Build();

await DbSeeder.MigrateAndSeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
