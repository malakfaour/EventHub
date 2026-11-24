using EventHub.Web.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EventHub.Web.Services;

/// <summary>Healthy when EventHubDb answers.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    public DatabaseHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await _db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy("EventHubDb is reachable")
            : HealthCheckResult.Unhealthy("Cannot connect to EventHubDb");
}
