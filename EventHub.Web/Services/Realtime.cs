using EventHub.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EventHub.Web.Services;

/// <summary>Pushes a short notification to one signed-in user's open browser tabs. Services depend on this, not on SignalR.</summary>
public interface IRealtimeNotifier
{
    Task NotifyAsync(Guid userId, string kind, string title, string message, string url);
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

    public async Task NotifyAsync(Guid userId, string kind, string title, string message, string url)
    {
        try
        {
            await _hub.Clients.Group(LiveHub.GroupFor(userId)).SendAsync("notify", new { kind, title, message, url });
        }
        catch (Exception ex)
        {
            // A missed live update must never fail the message that caused it; the page still shows the data on refresh.
            _logger.LogWarning(ex, "Live notification to {UserId} failed", userId);
        }
    }
}
