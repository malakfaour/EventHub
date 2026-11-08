using EventHub.Admin.Data;
using EventHub.Admin.Messaging;
using EventHub.Admin.Models;
using EventHub.Admin.Repositories;
using EventHub.Admin.Services;
using EventHub.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/admin-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<StaffUser, IdentityRole<Guid>>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Distinct cookie names: both apps run on localhost, and browsers share cookies across ports.
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "EventHub.Admin.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddAntiforgery(o => o.Cookie.Name = "EventHub.Admin.Antiforgery");

// Role-based policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanModerate", p => p.RequireRole(Roles.Admin, Roles.Moderator));

// Messaging: RabbitMQ, outbox, idempotency, message log, consumers
builder.Services.AddRabbitMessaging(builder.Configuration);
builder.Services.AddScoped<EfOutbox>();
builder.Services.AddScoped<IOutbox>(sp => sp.GetRequiredService<EfOutbox>());
builder.Services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<EfOutbox>());
builder.Services.AddScoped<IIdempotencyGuard, EfIdempotencyGuard>();
builder.Services.AddScoped<IMessageLog, EfMessageLog>();
builder.Services.AddOutboxPublisher();
builder.Services.AddConsumer<ModerationConsumer>();

builder.Services.AddScoped<IModerationRepository, ModerationRepository>();
builder.Services.AddScoped<IModerationService, ModerationService>();

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
        pattern: "{controller=Moderation}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
