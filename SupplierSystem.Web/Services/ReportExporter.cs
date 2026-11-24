using ClosedXML.Excel;
using SupplierSystem.Web.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SupplierSystem.Web.Services;

/// <summary>Builds the downloadable Excel workbook and PDF summary of a supplier report.</summary>
public static class ReportExporter
{
    private static List<LabelValue> Summary(SupplierReport r) =>
    [
        new("Orders received", r.Orders), new("Orders delivered", r.Delivered), new("Revenue (delivered)", r.Revenue),
        new("Acceptance rate %", r.AcceptanceRate), new("Invoiced", r.Invoiced),
        new("Collected", r.Collected), new("Outstanding", r.Outstanding)
    ];

    public static byte[] ToExcel(SupplierReport r)
    {
        using var wb = new XLWorkbook();

        var s = wb.AddWorksheet("Summary");
        s.Cell(1, 1).Value = "Report for";
        s.Cell(1, 2).Value = r.Scope;
        s.Cell(2, 1).Value = "Generated (UTC)";
        s.Cell(2, 2).Value = r.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm");
        var row = 4;
        s.Cell(row, 1).Value = "Metric";
        s.Cell(row, 2).Value = "Value";
        s.Row(row).Style.Font.Bold = true;
        foreach (var item in Summary(r))
        {
            row++;
            s.Cell(row, 1).Value = item.Label;
            s.Cell(row, 2).Value = item.Value;
        }
        s.Columns().AdjustToContents();

        Sheet(wb, "Orders", r.OrderRows.Select(o => new
        {
            Supplier = o.SupplierName, Event = o.EventName, o.Customer, Source = o.Source.ToString(),
            Status = o.Status.ToString(), o.Total,
            Received = o.ReceivedAtUtc.ToString("yyyy-MM-dd HH:mm"), Delivery = o.DeliveryAtUtc.ToString("yyyy-MM-dd HH:mm")
        }));
        Sheet(wb, "Invoices", r.InvoiceRows.Select(i => new
        {
            i.Number, Event = i.EventName, i.Amount, Status = i.Status.ToString(),
            Issued = i.IssuedAtUtc.ToString("yyyy-MM-dd"), Due = i.DueAtUtc.ToString("yyyy-MM-dd"),
            Paid = i.PaidAtUtc?.ToString("yyyy-MM-dd") ?? ""
        }));
        Sheet(wb, "Revenue by product", r.RevenueByProduct.Select(p => new { Product = p.Label, Revenue = p.Value }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Sheet<T>(XLWorkbook wb, string name, IEnumerable<T> data)
    {
        var ws = wb.AddWorksheet(name);
        var list = data.ToList();
        if (list.Count > 0) ws.Cell(1, 1).InsertTable(list, false);
        ws.Columns().AdjustToContents();
    }

    public static byte[] ToPdf(SupplierReport r)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Column(h =>
            {
                h.Item().Text($"Supplier report - {r.Scope}").FontSize(20).SemiBold();
                h.Item().Text($"Generated {r.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(14);
                Section(col, "Overview", Summary(r), "Value");
                Section(col, "Orders by month", r.OrdersByMonth, "Orders");
                Section(col, "Orders by status", r.OrdersByStatus, "Orders");
                Section(col, "Revenue by product (delivered orders)", r.RevenueByProduct, "Revenue");
                Section(col, "Invoices", r.InvoiceAmounts, "Amount");
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
            });
        })).GeneratePdf();
    }

    private static void Section(ColumnDescriptor col, string title, List<LabelValue> rows, string valueHeader)
    {
        col.Item().Column(c =>
        {
            c.Item().Text(title).FontSize(13).SemiBold();
            if (rows.Count == 0)
            {
                c.Item().Text("No data yet.").FontColor(Colors.Grey.Darken1);
                return;
            }
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(d => { d.RelativeColumn(3); d.RelativeColumn(1); });
                t.Header(h =>
                {
                    h.Cell().BorderBottom(1).PaddingVertical(3).Text("Item").SemiBold();
                    h.Cell().BorderBottom(1).PaddingVertical(3).AlignRight().Text(valueHeader).SemiBold();
                });
                foreach (var row in rows)
                {
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(row.Label);
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).AlignRight()
                        .Text(row.Value.ToString("0.##"));
                }
            });
        });
    }
}
