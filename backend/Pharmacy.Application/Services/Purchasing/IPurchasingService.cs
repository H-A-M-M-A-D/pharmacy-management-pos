using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Purchasing;

public interface IPurchasingService
{
    Task<PagedResult<PurchaseOrderListItemDto>> ListPurchaseOrdersAsync(Guid actorId, PurchaseOrderListQuery query, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailsDto> GetPurchaseOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailsDto> CreatePurchaseOrderAsync(Guid actorId, PurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailsDto> UpdatePurchaseOrderAsync(Guid actorId, Guid id, PurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailsDto> SubmitPurchaseOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailsDto> CancelPurchaseOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PurchaseHistoryItemDto>> ListPurchasesAsync(Guid actorId, PurchaseHistoryQuery query, CancellationToken cancellationToken = default);
    Task<GoodsReceiptDetailsDto> GetGoodsReceiptAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<GoodsReceiptDetailsDto> PostGoodsReceiptAsync(Guid actorId, GoodsReceiptRequest request, CancellationToken cancellationToken = default);
    Task<PurchasingOptionsDto> GetOptionsAsync(Guid actorId, string? productSearch, CancellationToken cancellationToken = default);
    Task<ReturnableGoodsReceiptDto> GetReturnableGoodsReceiptAsync(Guid actorId, Guid goodsReceiptId, CancellationToken cancellationToken = default);
    Task<PurchaseReturnDetailsDto> PostPurchaseReturnAsync(Guid actorId, Guid goodsReceiptId, PostPurchaseReturnRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<PurchaseReturnListItemDto>> ListPurchaseReturnsAsync(Guid actorId, PurchaseReturnListQuery query, CancellationToken cancellationToken = default);
    Task<PurchaseReturnDetailsDto> GetPurchaseReturnAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseReturnDetailsDto> ReprintPurchaseReturnAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
}
