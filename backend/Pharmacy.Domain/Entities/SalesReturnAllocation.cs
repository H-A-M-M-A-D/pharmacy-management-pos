using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SalesReturnAllocation : Entity
{
    public Guid SalesReturnItemId { get; set; }
    public SalesReturnItem? SalesReturnItem { get; set; }
    public Guid OriginalSaleItemBatchAllocationId { get; set; }
    public SaleItemBatchAllocation? OriginalSaleItemBatchAllocation { get; set; }
    public Guid ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
    public int Quantity { get; set; }
    public SalesReturnDisposition Disposition { get; set; }
    public decimal UnitRetailPriceSnapshot { get; set; }
    public decimal UnitSalePriceSnapshot { get; set; }
    public decimal UnitCostPriceSnapshot { get; set; }
    public DateOnly ExpiryDateSnapshot { get; set; }
    public decimal GrossReturnAmount { get; set; }
    public decimal DiscountReturnAmount { get; set; }
    public decimal TaxReturnAmount { get; set; }
    public decimal RefundAmount { get; set; }
}

public enum SalesReturnDisposition
{
    Restockable = 1,
    NonResellable = 2
}
