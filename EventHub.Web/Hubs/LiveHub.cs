using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EventHub.Web.Hubs;

/// <summary>One group per signed-in user, so a notification only reaches the person it is about.</summary>
[Authorize]
public sealed class LiveHub : Hub
{
    public static string GroupFor(Guid userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(id));
        await base.OnConnectedAsync();
    }
}
