using Microsoft.AspNetCore.SignalR;
using SupplierSystem.Web.Hubs;

namespace SupplierSystem.Web.Services;

/// <summary>Pushes a short notification to a supplier's open browser tabs (and to the platform admin).</summary>
public interface IRealtimeNotifier
{
    Task NotifySupplierAsync(Guid supplierId, string kind, string title, string message, string url);
}

public sealed class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<LiveHub> _hub;
    private readonly ILogger<SignalRNotifier> _logger;

    public SignalRNotifier(IHubContext<LiveHub> hub, ILogger<SignalRNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifySupplierAsync(Guid supplierId, string kind, string title, string message, string url)
    {
        try
        {
            await _hub.Clients.Groups(LiveHub.GroupFor(supplierId), LiveHub.AllSuppliers)
                .SendAsync("notify", new { kind, title, message, url });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live notification for supplier {SupplierId} failed", supplierId);
        }
    }
}
