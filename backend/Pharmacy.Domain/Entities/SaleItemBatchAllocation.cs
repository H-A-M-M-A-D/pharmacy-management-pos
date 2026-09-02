using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SaleItemBatchAllocation : Entity
{
    public Guid SaleItemId { get; set; }
    public SaleItem? SaleItem { get; set; }
    public Guid ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
    public int Quantity { get; set; }
    public decimal UnitRetailPriceSnapshot { get; set; }
    public decimal UnitSalePriceSnapshot { get; set; }
    public decimal UnitCostPriceSnapshot { get; set; }
    public DateOnly ExpiryDateSnapshot { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
}
