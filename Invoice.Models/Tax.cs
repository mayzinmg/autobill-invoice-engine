namespace Invoice.Models.Tax
{
    public record TaxComponent(string Name, decimal Rate, bool IncludedInPrice = false);

    public record TaxRule(
        string Country,            // "SG","DE","US"
        string? Region,            // e.g. "CA" (optional)
        string? ProductCategory,   // e.g. "standard","reduced" (optional)
        string? CustomerType,      // "Company","Individual" (optional)
        List<TaxComponent> Components
    );

    public record TaxBreakdown(string Name, decimal Amount);

}
