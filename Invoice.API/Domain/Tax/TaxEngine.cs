using Invoice.Models.Tax;
using System.Collections.Concurrent;
namespace Invoice.API.Domain.Tax
{
    public interface ITaxEngine
    {
        List<TaxComponent> Resolve(string country, string? region, string? productCategory, string? customerType);
        (decimal taxTotal, List<TaxBreakdown> breakdown) Calculate(decimal netAmount, IEnumerable<TaxComponent> components);
    }
    public class TaxEngine: ITaxEngine
    {
        private readonly List<TaxRule> _rules = new()
    {
        // Singapore GST 9%
        new("SG", null, null, null, new() { new("GST", 0.09m) }),

        // Germany VAT (standard 19%, reduced 7% via category)
        new("DE", null, "standard", null, new() { new("VAT", 0.19m) }),
        new("DE", null, "reduced",  null, new() { new("VAT", 0.07m) }),

        // US California example: state + city
        new("US", "CA", null, null, new() { new("StateTax", 0.0725m), new("CityTax", 0.025m) })
    };

        public List<TaxComponent> Resolve(string country, string? region, string? productCategory, string? customerType)
        {
            var rule = _rules.FirstOrDefault(r =>
                r.Country == country &&
                (r.Region == null || r.Region == region) &&
                (r.ProductCategory == null || r.ProductCategory == productCategory) &&
                (r.CustomerType == null || r.CustomerType == customerType));

            return rule?.Components ?? new();
        }

        public (decimal taxTotal, List<TaxBreakdown> breakdown) Calculate(decimal netAmount, IEnumerable<TaxComponent> components)
        {
            var map = new ConcurrentDictionary<string, decimal>();
            foreach (var c in components)
            {
                var amt = Math.Round(netAmount * c.Rate, 2, MidpointRounding.AwayFromZero);
                map.AddOrUpdate(c.Name, amt, (_, existing) => existing + amt);
            }
            var list = map.Select(kv => new TaxBreakdown(kv.Key, kv.Value)).ToList();
            return (list.Sum(x => x.Amount), list);
        }
    }
}

