
using Invoice.Models.InvoicePdf;
namespace Invoice.PDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;        // for Colors.*
using QuestPDF.Infrastructure; // for PageSizes, fonts, etc.

public class InvoiceGenerator
{
    public static class InvoicePdfComposer
    {
        public static Task<byte[]> RenderAsync(InvoicePdf model)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    // Header
                    page.Header()
                        .Text($"Invoice #{model.InvoiceId}")
                        .FontSize(20)
                        .Bold()
                        .AlignLeft();

                    // Customer
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Text($"Customer: {model.CustomerName}");
                        col.Item().Text($"Date: {System.DateTime.UtcNow:yyyy-MM-dd}");
                        col.Item().Text("");

                        // Items Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(5); // Description
                                columns.RelativeColumn(2); // Qty
                                columns.RelativeColumn(3); // Price
                            });

                            // Header row
                            table.Header(header =>
                            {
                                header.Cell().Text("Description").Bold();
                                header.Cell().Text("Qty").Bold();
                                header.Cell().Text("Price").Bold();
                            });

                            // Data rows
                            foreach (var item in model.Items)
                            {
                                table.Cell().Text(item.Description);
                                table.Cell().Text(item.Quantity.ToString());
                                table.Cell().Text(item.UnitPrice.ToString("C"));
                            }
                        });

                        col.Item().Text("");

                        // Subtotal
                        col.Item().Text($"Subtotal: {model.Subtotal:C}");

                        // Tax breakdown
                        foreach (var tax in model.TaxBreakdown)
                            col.Item().Text($"{tax.Name}: {tax.Amount:C}");

                        // Grand total
                        col.Item().Text($"Grand Total: {model.GrandTotal:C}").Bold();
                    });

                    page.Footer().AlignCenter().Text($"Generated on {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return Task.FromResult(ms.ToArray());
        }
    }
}
