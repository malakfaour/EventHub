using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EventHub.Messaging;

/// <summary>Healthy when the shared RabbitMQ connection is open. Used by /health in both apps.</summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IRabbitMqConnection _connection;
    public RabbitMqHealthCheck(IRabbitMqConnection connection) => _connection = connection;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var connection = await _connection.GetAsync(timeout.Token);
            return connection.IsOpen
                ? HealthCheckResult.Healthy("Connected to RabbitMQ")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed");
        }
        catch (Exception ex)
        {
            // Messages keep waiting in the outbox while the broker is down, so this is degraded, not fatal.
            return HealthCheckResult.Degraded("RabbitMQ is unreachable: " + ex.Message);
        }
    }
}

/// <summary>Writes the /health result as small JSON: overall status plus one entry per check.</summary>
public static class HealthReportWriter
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            durationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), description = e.Value.Description })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }
}
