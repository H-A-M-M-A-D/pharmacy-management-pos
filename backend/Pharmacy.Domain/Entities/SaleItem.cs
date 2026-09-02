using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SaleItem : Entity
{
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int RequestedQuantity { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public ICollection<SaleItemBatchAllocation> Allocations { get; set; } = new List<SaleItemBatchAllocation>();
}
