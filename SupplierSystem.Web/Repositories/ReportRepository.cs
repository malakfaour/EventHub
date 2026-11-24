using Microsoft.EntityFrameworkCore;
using SupplierSystem.Web.Data;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Repositories;

/// <summary>Read-only projections for the reports. supplierId null = every supplier (platform admin).</summary>
public interface IReportRepository
{
    Task<List<ReportOrderRow>> ListOrdersAsync(Guid? supplierId, CancellationToken ct = default);
    Task<List<ReportLineRow>> ListLinesAsync(Guid? supplierId, CancellationToken ct = default);
    Task<List<ReportInvoiceRow>> ListInvoicesAsync(Guid? supplierId, CancellationToken ct = default);
    Task<string> GetScopeNameAsync(Guid? supplierId, CancellationToken ct = default);
}

public sealed class ReportRepository : IReportRepository
{
    private readonly AppDbContext _db;
    public ReportRepository(AppDbContext db) => _db = db;

    public Task<List<ReportOrderRow>> ListOrdersAsync(Guid? supplierId, CancellationToken ct = default) =>
        _db.Orders.AsNoTracking()
            .Where(o => supplierId == null || o.SupplierId == supplierId)
            .OrderByDescending(o => o.ReceivedAtUtc)
            .Select(o => new ReportOrderRow(o.Id, o.Supplier!.Name, o.EventName, o.Customer!.Name, o.Source, o.Status,
                o.Total, o.ReceivedAtUtc, o.DeliveryAtUtc))
            .ToListAsync(ct);

    public async Task<List<ReportLineRow>> ListLinesAsync(Guid? supplierId, CancellationToken ct = default)
    {
        var lines = await _db.Orders.AsNoTracking()
            .Where(o => supplierId == null || o.SupplierId == supplierId)
            .SelectMany(o => o.Lines.Select(l => new { l.Name, o.Status, l.Quantity, l.UnitPrice }))
            .ToListAsync(ct);
        return lines.Select(l => new ReportLineRow(l.Name, l.Status, l.Quantity, l.Quantity * l.UnitPrice)).ToList();
    }

    public async Task<List<ReportInvoiceRow>> ListInvoicesAsync(Guid? supplierId, CancellationToken ct = default)
    {
        var rows = await _db.Invoices.AsNoTracking()
            .Where(i => supplierId == null || i.SupplierId == supplierId)
            .OrderByDescending(i => i.IssuedAtUtc)
            .Select(i => new { i.Number, i.Order!.EventName, i.Amount, i.Status, i.IssuedAtUtc, i.DueAtUtc, i.PaidAtUtc })
            .ToListAsync(ct);
        return rows.Select(i => new ReportInvoiceRow(i.Number, i.EventName, i.Amount, i.Status, i.IssuedAtUtc, i.DueAtUtc, i.PaidAtUtc)).ToList();
    }

    public async Task<string> GetScopeNameAsync(Guid? supplierId, CancellationToken ct = default) =>
        supplierId is null
            ? "All suppliers"
            : await _db.Suppliers.Where(s => s.Id == supplierId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "Supplier";
}
