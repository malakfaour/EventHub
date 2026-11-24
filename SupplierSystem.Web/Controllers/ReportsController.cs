using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Services;

namespace SupplierSystem.Web.Controllers;

/// <summary>Managers and staff see their own supplier's figures; the platform admin sees all suppliers together.</summary>
[Authorize]
public class ReportsController : StaffControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _reports.GetAsync(SupplierScope, ct));

    public async Task<IActionResult> ExportExcel(CancellationToken ct) =>
        File(await _reports.ExportExcelAsync(SupplierScope, ct),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"supplier-report-{DateTime.UtcNow:yyyyMMdd}.xlsx");

    public async Task<IActionResult> ExportPdf(CancellationToken ct) =>
        File(await _reports.ExportPdfAsync(SupplierScope, ct), "application/pdf",
            $"supplier-report-{DateTime.UtcNow:yyyyMMdd}.pdf");
}
