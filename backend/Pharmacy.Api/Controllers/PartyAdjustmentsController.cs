using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.PartyAdjustments;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class PartyAdjustmentsController(IPartyAdjustmentService adjustments) : ControllerBase
{
    [HttpPost("supplier-adjustments/{sourceType}/{id:guid}/reverse"), HasPermission(PermissionCatalog.AccountsJournalReverse)]
    public Task<SupplierAdjustmentReversalDto> ReverseSupplierAdjustment(Pharmacy.Domain.Entities.JournalSourceType sourceType, Guid id, ReverseSupplierAdjustmentRequest request, CancellationToken ct) =>
        adjustments.ReverseSupplierAdjustmentAsync(UserId(), sourceType, id, request, ct);
    [HttpGet("credit-notes"), HasPermission(PermissionCatalog.AccountsCreditNotesView)]
    public Task<IReadOnlyList<CreditNoteDto>> ListCreditNotes([FromQuery] Guid? customerId, [FromQuery] Guid? branchId, CancellationToken ct) =>
        adjustments.ListCreditNotesAsync(UserId(), customerId, branchId, ct);

    [HttpPost("credit-notes"), HasPermission(PermissionCatalog.AccountsCreditNotesCreate)]
    public Task<CreditNoteDto> CreateCreditNote(CreateCreditNoteRequest request, CancellationToken ct) => adjustments.CreateCreditNoteAsync(UserId(), request, ct);

    [HttpGet("debit-notes"), HasPermission(PermissionCatalog.AccountsDebitNotesView)]
    public Task<IReadOnlyList<DebitNoteDto>> ListDebitNotes([FromQuery] Guid? supplierId, [FromQuery] Guid? branchId, CancellationToken ct) =>
        adjustments.ListDebitNotesAsync(UserId(), supplierId, branchId, ct);

    [HttpPost("debit-notes"), HasPermission(PermissionCatalog.AccountsDebitNotesCreate)]
    public Task<DebitNoteDto> CreateDebitNote(CreateDebitNoteRequest request, CancellationToken ct) => adjustments.CreateDebitNoteAsync(UserId(), request, ct);

    [HttpGet("customer-writeoffs"), HasPermission(PermissionCatalog.AccountsWriteOffsView)]
    public Task<IReadOnlyList<CustomerWriteOffDto>> ListCustomerWriteOffs([FromQuery] Guid? customerId, [FromQuery] Guid? branchId, CancellationToken ct) =>
        adjustments.ListCustomerWriteOffsAsync(UserId(), customerId, branchId, ct);

    [HttpPost("customer-writeoffs"), HasPermission(PermissionCatalog.AccountsWriteOffsCreate)]
    public Task<CustomerWriteOffDto> CreateCustomerWriteOff(CreateCustomerWriteOffRequest request, CancellationToken ct) => adjustments.CreateCustomerWriteOffAsync(UserId(), request, ct);

    [HttpGet("supplier-writeoffs"), HasPermission(PermissionCatalog.AccountsWriteOffsView)]
    public Task<IReadOnlyList<SupplierWriteOffDto>> ListSupplierWriteOffs([FromQuery] Guid? supplierId, [FromQuery] Guid? branchId, CancellationToken ct) =>
        adjustments.ListSupplierWriteOffsAsync(UserId(), supplierId, branchId, ct);

    [HttpPost("supplier-writeoffs"), HasPermission(PermissionCatalog.AccountsWriteOffsCreate)]
    public Task<SupplierWriteOffDto> CreateSupplierWriteOff(CreateSupplierWriteOffRequest request, CancellationToken ct) => adjustments.CreateSupplierWriteOffAsync(UserId(), request, ct);

    [HttpGet("customer-advances"), HasPermission(PermissionCatalog.AccountsAdvancesView)]
    public Task<IReadOnlyList<CustomerAdvanceDto>> ListCustomerAdvances([FromQuery] Guid? customerId, [FromQuery] Guid? branchId, CancellationToken ct) =>
        adjustments.ListCustomerAdvancesAsync(UserId(), customerId, branchId, ct);

    [HttpPost("customer-advances"), HasPermission(PermissionCatalog.AccountsAdvancesCreate)]
    public Task<CustomerAdvanceDto> RecordCustomerAdvance(RecordCustomerAdvanceRequest request, CancellationToken ct) => adjustments.RecordCustomerAdvanceAsync(UserId(), request, ct);

    [HttpPost("customer-advances/{id:guid}/apply"), HasPermission(PermissionCatalog.AccountsAdvancesApply)]
    public Task<CustomerAdvanceDto> ApplyCustomerAdvance(Guid id, ApplyCustomerAdvanceRequest request, CancellationToken ct) => adjustments.ApplyCustomerAdvanceAsync(UserId(), id, request, ct);

    [HttpGet("supplier-advances"), HasPermission(PermissionCatalog.AccountsAdvancesView)]
    public Task<IReadOnlyList<SupplierAdvanceDto>> ListSupplierAdvances([FromQuery] Guid? supplierId, [FromQuery] Guid? branchId, CancellationToken ct) =>
        adjustments.ListSupplierAdvancesAsync(UserId(), supplierId, branchId, ct);

    [HttpPost("supplier-advances"), HasPermission(PermissionCatalog.AccountsAdvancesCreate)]
    public Task<SupplierAdvanceDto> RecordSupplierAdvance(RecordSupplierAdvanceRequest request, CancellationToken ct) => adjustments.RecordSupplierAdvanceAsync(UserId(), request, ct);

    [HttpPost("supplier-advances/{id:guid}/apply"), HasPermission(PermissionCatalog.AccountsAdvancesApply)]
    public Task<SupplierAdvanceDto> ApplySupplierAdvance(Guid id, ApplySupplierAdvanceRequest request, CancellationToken ct) => adjustments.ApplySupplierAdvanceAsync(UserId(), id, request, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
