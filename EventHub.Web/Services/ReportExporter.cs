using ClosedXML.Excel;
using EventHub.Web.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EventHub.Web.Services;

/// <summary>Builds the downloadable Excel workbook and PDF summary from report data.</summary>
public static class ReportExporter
{
    public static byte[] ToExcel(ReportData r, List<AdminEventRow> events, List<AdminOrderRow> orders, List<AdminPurchaseRow> purchases)
    {
        using var wb = new XLWorkbook();

        var s = wb.AddWorksheet("Summary");
        var rows = SummaryRows(r);
        s.Cell(1, 1).Value = "Metric";
        s.Cell(1, 2).Value = "Value";
        for (var i = 0; i < rows.Count; i++)
        {
            s.Cell(i + 2, 1).Value = rows[i].Label;
            s.Cell(i + 2, 2).Value = rows[i].Value;
        }
        s.Row(1).Style.Font.Bold = true;
        s.Columns().AdjustToContents();

        Sheet(wb, "Events", events.Select(e => new
        {
            e.Name, e.Organizer, e.Venue, Starts = e.StartsAtUtc.ToString("yyyy-MM-dd HH:mm"),
            Status = e.Status.ToString(), Published = e.IsPublished ? "Yes" : "No",
            Guests = e.ExpectedGuests, e.Orders, e.TicketsSold
        }));
        Sheet(wb, "Supply orders", orders.Select(o => new
        {
            Event = o.EventName, o.SupplierName, Category = o.Category, Status = o.Status.ToString(),
            o.Total, Placed = o.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")
        }));
        Sheet(wb, "Ticket sales", purchases.Select(p => new
        {
            Event = p.EventName, p.Client, Ticket = p.TicketType, p.Quantity, p.Total,
            Status = p.Status.ToString(), Date = p.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")
        }));

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

    private static List<LabelValue> SummaryRows(ReportData r) =>
    [
        new("Users", r.Users), new("Organizers", r.Organizers), new("Clients", r.Clients),
        new("Events", r.Events), new("Published events", r.PublishedEvents),
        new("Supply orders", r.Orders), new("Tickets sold", r.TicketsSold),
        new("Ticket revenue", r.TicketRevenue), new("Supply spend (active orders)", r.SupplySpend),
        new("Rejection rate %", r.RejectionRate), new("Cancellation rate %", r.CancellationRate)
    ];

    public static byte[] ToPdf(ReportData r)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Column(h =>
            {
                h.Item().Text("Event Hub - platform report").FontSize(20).SemiBold();
                h.Item().Text($"Generated {r.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(14);
                Section(col, "Overview", SummaryRows(r), "Value");
                Section(col, "Events by status", r.EventsByStatus, "Events");
                Section(col, "Events by month", r.EventsByMonth, "Events");
                Section(col, "Supply orders by category", r.OrdersByCategory, "Orders");
                Section(col, "Supply spend by category", r.SpendByCategory, "Amount");
                Section(col, "Supply orders by status", r.OrdersByStatus, "Orders");
                Section(col, "Top events by ticket revenue", r.RevenueByEvent, "Revenue");
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
