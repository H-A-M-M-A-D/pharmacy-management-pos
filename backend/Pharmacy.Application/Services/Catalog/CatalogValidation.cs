using Pharmacy.Application.Common;

namespace Pharmacy.Application.Services.Catalog;

public static class CatalogValidation
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
    public static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : Normalize(value);

    public static void Product(string name, string unit, int packSize, decimal purchasePrice,
        decimal retailPrice, decimal? tradePrice, decimal discount, int reorderLevel)
    {
        Required(name, "Product name", 500);
        Required(unit, "Unit", 50);
        if (packSize < 1) throw new RequestValidationException("Pack size must be at least 1.");
        if (purchasePrice < 0 || retailPrice < 0 || tradePrice < 0)
            throw new RequestValidationException("Product prices cannot be negative.");
        if (discount is < 0 or > 100)
            throw new RequestValidationException("Maximum discount must be between 0 and 100 percent.");
        if (reorderLevel < 0) throw new RequestValidationException("Reorder level cannot be negative.");
    }

    public static void Sku(string sku) => Required(sku, "SKU", 100);
    public static void Name(string name, string label) => Required(name, label, 200);

    private static void Required(string value, string label, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new RequestValidationException($"{label} is required.");
        if (value.Trim().Length > maximum) throw new RequestValidationException($"{label} cannot exceed {maximum} characters.");
    }
}
