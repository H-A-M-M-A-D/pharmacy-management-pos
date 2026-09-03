using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SalesReturnItem : Entity
{
    public Guid SalesReturnId { get; set; }
    public SalesReturn? SalesReturn { get; set; }
    public Guid OriginalSaleItemId { get; set; }
    public SaleItem? OriginalSaleItem { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal GrossReturnAmount { get; set; }
    public decimal DiscountReturnAmount { get; set; }
    public decimal TaxReturnAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public ICollection<SalesReturnAllocation> Allocations { get; set; } = new List<SalesReturnAllocation>();
}
