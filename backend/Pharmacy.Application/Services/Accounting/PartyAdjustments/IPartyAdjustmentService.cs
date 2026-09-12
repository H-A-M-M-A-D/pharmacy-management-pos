using Pharmacy.Application.DTOs.Accounting;

namespace Pharmacy.Application.Services.Accounting.PartyAdjustments;

public interface IPartyAdjustmentService
{
    Task<SupplierAdjustmentReversalDto> ReverseSupplierAdjustmentAsync(Guid actorId, Pharmacy.Domain.Entities.JournalSourceType sourceType, Guid sourceId, ReverseSupplierAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditNoteDto>> ListCreditNotesAsync(Guid actorId, Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<CreditNoteDto> CreateCreditNoteAsync(Guid actorId, CreateCreditNoteRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DebitNoteDto>> ListDebitNotesAsync(Guid actorId, Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<DebitNoteDto> CreateDebitNoteAsync(Guid actorId, CreateDebitNoteRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerWriteOffDto>> ListCustomerWriteOffsAsync(Guid actorId, Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<CustomerWriteOffDto> CreateCustomerWriteOffAsync(Guid actorId, CreateCustomerWriteOffRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierWriteOffDto>> ListSupplierWriteOffsAsync(Guid actorId, Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<SupplierWriteOffDto> CreateSupplierWriteOffAsync(Guid actorId, CreateSupplierWriteOffRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerAdvanceDto>> ListCustomerAdvancesAsync(Guid actorId, Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<CustomerAdvanceDto> RecordCustomerAdvanceAsync(Guid actorId, RecordCustomerAdvanceRequest request, CancellationToken cancellationToken = default);
    Task<CustomerAdvanceDto> ApplyCustomerAdvanceAsync(Guid actorId, Guid advanceId, ApplyCustomerAdvanceRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierAdvanceDto>> ListSupplierAdvancesAsync(Guid actorId, Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<SupplierAdvanceDto> RecordSupplierAdvanceAsync(Guid actorId, RecordSupplierAdvanceRequest request, CancellationToken cancellationToken = default);
    Task<SupplierAdvanceDto> ApplySupplierAdvanceAsync(Guid actorId, Guid advanceId, ApplySupplierAdvanceRequest request, CancellationToken cancellationToken = default);
}
