using EventHub.Contracts;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Repositories;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Services;

public interface IReportService
{
    Task<SupplierReport> GetAsync(Guid? supplierScope, CancellationToken ct = default);
    Task<byte[]> ExportExcelAsync(Guid? supplierScope, CancellationToken ct = default);
    Task<byte[]> ExportPdfAsync(Guid? supplierScope, CancellationToken ct = default);
}

public sealed class ReportService : IReportService
{
    private readonly IReportRepository _repo;
    public ReportService(IReportRepository repo) => _repo = repo;

    public async Task<SupplierReport> GetAsync(Guid? supplierScope, CancellationToken ct = default)
    {
        var orders = await _repo.ListOrdersAsync(supplierScope, ct);
        var lines = await _repo.ListLinesAsync(supplierScope, ct);
        var invoices = await _repo.ListInvoicesAsync(supplierScope, ct);

        var delivered = orders.Where(o => o.Status == SupplyOrderStatus.Delivered).ToList();
        var accepted = orders.Count(o => o.Status is SupplyOrderStatus.Accepted or SupplyOrderStatus.Preparing
            or SupplyOrderStatus.Dispatched or SupplyOrderStatus.Delivered);
        var rejected = orders.Count(o => o.Status == SupplyOrderStatus.Rejected);
        var decided = accepted + rejected;

        var paid = invoices.Where(i => i.Status == InvoiceStatus.Paid).ToList();
        var open = invoices.Where(i => i.Status == InvoiceStatus.Issued).ToList();
        var overdue = open.Where(i => i.DueAtUtc < DateTime.UtcNow).ToList();

        return new SupplierReport
        {
            Scope = await _repo.GetScopeNameAsync(supplierScope, ct),
            Orders = orders.Count,
            Delivered = delivered.Count,
            Revenue = delivered.Sum(o => o.Total),
            AcceptanceRate = decided == 0 ? 0 : Math.Round(accepted * 100m / decided, 1),
            Invoiced = invoices.Sum(i => i.Amount),
            Collected = paid.Sum(i => i.Amount),
            Outstanding = open.Sum(i => i.Amount),
            OrdersByMonth = orders.GroupBy(o => o.ReceivedAtUtc.ToString("yyyy-MM")).OrderBy(g => g.Key)
                .Select(g => new LabelValue(g.Key, g.Count())).ToList(),
            OrdersByStatus = orders.GroupBy(o => o.Status.ToString())
                .Select(g => new LabelValue(g.Key, g.Count())).ToList(),
            RevenueByProduct = lines.Where(l => l.Status == SupplyOrderStatus.Delivered)
                .GroupBy(l => l.Product).Select(g => new LabelValue(g.Key, g.Sum(l => l.LineTotal)))
                .OrderByDescending(x => x.Value).Take(8).ToList(),
            InvoiceAmounts =
            [
                new LabelValue("Paid", paid.Sum(i => i.Amount)),
                new LabelValue("Awaiting payment", open.Except(overdue).Sum(i => i.Amount)),
                new LabelValue("Overdue", overdue.Sum(i => i.Amount))
            ],
            OrderRows = orders,
            InvoiceRows = invoices
        };
    }

    public async Task<byte[]> ExportExcelAsync(Guid? supplierScope, CancellationToken ct = default) =>
        ReportExporter.ToExcel(await GetAsync(supplierScope, ct));

    public async Task<byte[]> ExportPdfAsync(Guid? supplierScope, CancellationToken ct = default) =>
        ReportExporter.ToPdf(await GetAsync(supplierScope, ct));
}
