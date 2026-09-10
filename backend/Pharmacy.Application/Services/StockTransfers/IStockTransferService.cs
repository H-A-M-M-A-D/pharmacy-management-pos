using Pharmacy.Application.DTOs.StockTransfers;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.StockTransfers;

public interface IStockTransferService
{
    Task<PagedResult<StockTransferListItemDto>> ListTransfersAsync(Guid actorId, StockTransferListQuery query, CancellationToken cancellationToken = default);
    Task<StockTransferDetailsDto> GetTransferAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransferableBatchDto>> ListTransferableBatchesAsync(Guid actorId, Guid branchId, Guid godownId, Guid? productId, string? search, CancellationToken cancellationToken = default);

    Task<StockTransferDetailsDto> CreateTransferAsync(Guid actorId, StockTransferRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDetailsDto> UpdateTransferAsync(Guid actorId, Guid id, StockTransferRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDetailsDto> RequestTransferAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<StockTransferDetailsDto> ApproveTransferAsync(Guid actorId, Guid id, ApproveStockTransferRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDetailsDto> DispatchTransferAsync(Guid actorId, Guid id, DispatchStockTransferRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDetailsDto> ReceiveTransferAsync(Guid actorId, Guid id, ReceiveStockTransferRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDetailsDto> CancelTransferAsync(Guid actorId, Guid id, CancelStockTransferRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDetailsDto> ResolveDiscrepancyAsync(Guid actorId, Guid id, ResolveStockTransferDiscrepancyRequest request, CancellationToken cancellationToken = default);
}
