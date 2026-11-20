using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Hubs;

/// <summary>Staff join their own supplier's group; the platform admin joins the group that hears every supplier.</summary>
[Authorize]
public sealed class LiveHub : Hub
{
    public const string AllSuppliers = "suppliers-all";
    public static string GroupFor(Guid supplierId) => $"supplier-{supplierId}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.IsInRole(Roles.PlatformAdmin) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AllSuppliers);
        else if (Guid.TryParse(user?.FindFirstValue("supplier_id"), out var supplierId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(supplierId));
        await base.OnConnectedAsync();
    }
}
