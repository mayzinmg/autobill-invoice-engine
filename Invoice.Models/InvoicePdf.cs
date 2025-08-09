namespace Invoice.Models.InvoicePdf
{
    using System.Collections.Generic;

    public class InvoicePdf
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public List<(string Description, int Quantity, decimal UnitPrice)> Items { get; set; }
            = new();

        public decimal Subtotal { get; set; }
        public List<Tax.TaxBreakdown> TaxBreakdown { get; set; }= new();
        public decimal GrandTotal { get; set; }
    }
}
