using EventHub.Contracts;
using EventHub.Web.Models;
using EventHub.Web.Repositories;
using EventHub.Web.ViewModels;

namespace EventHub.Web.Services;

public interface IAdminService
{
    Task<ReportData> GetReportAsync(CancellationToken ct = default);
    Task<byte[]> ExportExcelAsync(CancellationToken ct = default);
    Task<byte[]> ExportPdfAsync(CancellationToken ct = default);

    /// <summary>Admin override: cancels an order on behalf of its organizer, with a reason that is sent to the supplier.</summary>
    Task CancelOrderAsync(Guid orderId, string reason, CancellationToken ct = default);
}

public class AdminService : IAdminService
{
    private readonly IAdminRepository _repo;
    private readonly IOrderService _orders;

    public AdminService(IAdminRepository repo, IOrderService orders)
    {
        _repo = repo;
        _orders = orders;
    }

    public async Task<ReportData> GetReportAsync(CancellationToken ct = default)
    {
        var users = await _repo.ListUsersAsync(ct);
        var events = await _repo.ListEventsAsync(ct);
        var orders = await _repo.ListOrdersAsync(ct);
        var purchases = await _repo.ListPurchasesAsync(ct);
        var paid = purchases.Where(p => p.Status == PurchaseStatus.Paid).ToList();
        var active = orders.Where(o => o.IsActive).ToList();

        decimal Rate(SupplyOrderStatus s) =>
            orders.Count == 0 ? 0 : Math.Round(orders.Count(o => o.Status == s) * 100m / orders.Count, 1);

        return new ReportData
        {
            Users = users.Count,
            Organizers = users.Count(u => u.Role == Roles.Organizer),
            Clients = users.Count(u => u.Role == Roles.Client),
            Events = events.Count,
            PublishedEvents = events.Count(e => e.IsPublished),
            Orders = orders.Count,
            TicketsSold = paid.Sum(p => p.Quantity),
            TicketRevenue = paid.Sum(p => p.Total),
            SupplySpend = active.Sum(o => o.Total),
            RejectionRate = Rate(SupplyOrderStatus.Rejected),
            CancellationRate = Rate(SupplyOrderStatus.Cancelled),
            EventsByStatus = events.GroupBy(e => e.Status.ToString())
                .Select(g => new LabelValue(g.Key, g.Count())).ToList(),
            EventsByMonth = events.GroupBy(e => e.StartsAtUtc.ToString("yyyy-MM")).OrderBy(g => g.Key)
                .Select(g => new LabelValue(g.Key, g.Count())).ToList(),
            OrdersByCategory = orders.GroupBy(o => SupplyCategories.DisplayName(o.Category))
                .Select(g => new LabelValue(g.Key, g.Count())).ToList(),
            SpendByCategory = active.GroupBy(o => SupplyCategories.DisplayName(o.Category))
                .Select(g => new LabelValue(g.Key, g.Sum(o => o.Total))).ToList(),
            OrdersByStatus = orders.GroupBy(o => o.Status.ToString())
                .Select(g => new LabelValue(g.Key, g.Count())).ToList(),
            RevenueByEvent = paid.GroupBy(p => p.EventName)
                .Select(g => new LabelValue(g.Key, g.Sum(p => p.Total)))
                .OrderByDescending(x => x.Value).Take(8).ToList()
        };
    }

    public async Task<byte[]> ExportExcelAsync(CancellationToken ct = default) =>
        ReportExporter.ToExcel(await GetReportAsync(ct), await _repo.ListEventsAsync(ct),
            await _repo.ListOrdersAsync(ct), await _repo.ListPurchasesAsync(ct));

    public async Task<byte[]> ExportPdfAsync(CancellationToken ct = default) =>
        ReportExporter.ToPdf(await GetReportAsync(ct));

    public async Task CancelOrderAsync(Guid orderId, string reason, CancellationToken ct = default)
    {
        var order = (await _repo.ListOrdersAsync(ct)).FirstOrDefault(o => o.Id == orderId)
                    ?? throw new InvalidOperationException("Order not found.");
        if (!order.CanBeCancelled) throw new InvalidOperationException("This order can no longer be cancelled.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required.");

        // Same cancel path as the organizer's (state change + OrderCancelled message in one transaction).
        await _orders.CancelAsAdminAsync(orderId, $"Cancelled by administrator: {reason.Trim()}", ct);
    }
}
