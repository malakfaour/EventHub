using EventHub.Web.Data;
using EventHub.Web.Models;
using EventHub.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Web.Repositories;

/// <summary>Read-only queries for the admin area. Projects straight to rows so nothing is tracked.</summary>
public interface IAdminRepository
{
    Task<List<AdminUserRow>> ListUsersAsync(CancellationToken ct = default);
    Task<List<AdminEventRow>> ListEventsAsync(CancellationToken ct = default);
    Task<List<AdminOrderRow>> ListOrdersAsync(CancellationToken ct = default);
    Task<List<AdminPurchaseRow>> ListPurchasesAsync(CancellationToken ct = default);
    Task<MessageMonitorData> GetMessagesAsync(string? status, CancellationToken ct = default);
}

public class AdminRepository : IAdminRepository
{
    private readonly AppDbContext _db;
    public AdminRepository(AppDbContext db) => _db = db;

    public Task<List<AdminUserRow>> ListUsersAsync(CancellationToken ct = default) =>
        (from u in _db.Users
         join ur in _db.UserRoles on u.Id equals ur.UserId into urs
         from ur in urs.DefaultIfEmpty()
         join r in _db.Roles on ur.RoleId equals r.Id into rs
         from r in rs.DefaultIfEmpty()
         orderby r.Name, u.Email
         select new AdminUserRow(u.Id, u.Email ?? "", u.DisplayName, r.Name ?? "-", u.Organization, u.LockoutEnd))
        .AsNoTracking().ToListAsync(ct);

    public Task<List<AdminEventRow>> ListEventsAsync(CancellationToken ct = default) =>
        (from e in _db.Events
         join u in _db.Users on e.OrganizerId equals u.Id into g
         from u in g.DefaultIfEmpty()
         orderby e.StartsAtUtc descending
         select new AdminEventRow(e.Id, e.Name, u.DisplayName ?? u.Email ?? "?", e.Venue, e.StartsAtUtc,
             e.Status, e.IsPublished, e.ExpectedGuests,
             _db.Orders.Count(o => o.EventId == e.Id),
             _db.Purchases.Where(p => p.EventId == e.Id && p.Status == PurchaseStatus.Paid).Sum(p => (int?)p.Quantity) ?? 0))
        .AsNoTracking().ToListAsync(ct);

    public Task<List<AdminOrderRow>> ListOrdersAsync(CancellationToken ct = default) =>
        _db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new AdminOrderRow(o.Id, o.EventId, o.Event!.Name, o.SupplierName, o.Category,
                o.Status, o.Total, o.CreatedAtUtc, o.UpdatedAtUtc))
            .ToListAsync(ct);

    public Task<List<AdminPurchaseRow>> ListPurchasesAsync(CancellationToken ct = default) =>
        (from p in _db.Purchases
         join u in _db.Users on p.ClientId equals u.Id into g
         from u in g.DefaultIfEmpty()
         orderby p.CreatedAtUtc descending
         select new AdminPurchaseRow(p.Id, p.Event!.Name, u.DisplayName ?? u.Email ?? "?", p.TicketTypeName,
             p.Quantity, p.Total, p.Status, p.CreatedAtUtc))
        .AsNoTracking().ToListAsync(ct);

    public async Task<MessageMonitorData> GetMessagesAsync(string? status, CancellationToken ct = default)
    {
        var data = new MessageMonitorData();
        var counts = await _db.MessageLog.GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        data.ByStatus = counts.ToDictionary(c => c.Status, c => c.Count);
        data.TotalLogged = counts.Sum(c => c.Count);
        data.OutboxPending = await _db.OutboxMessages.CountAsync(o => o.PublishedAtUtc == null, ct);
        data.OutboxRetrying = await _db.OutboxMessages.CountAsync(o => o.PublishedAtUtc == null && o.Attempts > 0, ct);
        data.Processed = await _db.ProcessedMessages.CountAsync(ct);

        var q = _db.MessageLog.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(m => m.Status == status);
        data.Rows = await q.OrderByDescending(m => m.Id).Take(200).ToListAsync(ct);
        return data;
    }
}
