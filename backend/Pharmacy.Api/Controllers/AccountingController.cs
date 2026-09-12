using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountingController(IAccountingService accounting) : ControllerBase
{
    [HttpGet("chart"), HasPermission(PermissionCatalog.AccountsCoaView)]
    public Task<IReadOnlyList<ChartOfAccountListItemDto>> ListAccounts([FromQuery] bool includeInactive, CancellationToken ct) =>
        accounting.ListAccountsAsync(UserId(), includeInactive, ct);

    [HttpGet("chart/{id:guid}"), HasPermission(PermissionCatalog.AccountsCoaView)]
    public Task<ChartOfAccountDto> GetAccount(Guid id, CancellationToken ct) => accounting.GetAccountAsync(UserId(), id, ct);

    [HttpPost("chart"), HasPermission(PermissionCatalog.AccountsCoaManage)]
    public Task<ChartOfAccountDto> CreateAccount(ChartOfAccountRequest request, CancellationToken ct) =>
        accounting.CreateAccountAsync(UserId(), request, ct);

    [HttpPut("chart/{id:guid}"), HasPermission(PermissionCatalog.AccountsCoaManage)]
    public Task<ChartOfAccountDto> UpdateAccount(Guid id, ChartOfAccountUpdateRequest request, CancellationToken ct) =>
        accounting.UpdateAccountAsync(UserId(), id, request, ct);

    [HttpPost("chart/{id:guid}/activate"), HasPermission(PermissionCatalog.AccountsCoaManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) { await accounting.SetAccountActiveAsync(UserId(), id, true, ct); return NoContent(); }

    [HttpPost("chart/{id:guid}/deactivate"), HasPermission(PermissionCatalog.AccountsCoaManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) { await accounting.SetAccountActiveAsync(UserId(), id, false, ct); return NoContent(); }

    [HttpGet("mappings"), HasPermission(PermissionCatalog.AccountsCoaView)]
    public Task<IReadOnlyList<AccountMappingDto>> ListMappings(CancellationToken ct) => accounting.ListAccountMappingsAsync(UserId(), ct);

    [HttpPost("mappings"), HasPermission(PermissionCatalog.AccountsCoaManage)]
    public Task<AccountMappingDto> SetMapping(SetAccountMappingRequest request, CancellationToken ct) =>
        accounting.SetAccountMappingAsync(UserId(), request, ct);

    [HttpPost("journal"), HasPermission(PermissionCatalog.AccountsJournalPost)]
    public Task<JournalEntryDto> PostJournal(PostManualJournalRequest request, CancellationToken ct) =>
        accounting.PostManualJournalAsync(UserId(), request, ct);

    [HttpGet("journal"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<PagedResult<JournalEntryListItemDto>> ListJournal([FromQuery] JournalEntryListQuery query, CancellationToken ct) =>
        accounting.ListJournalEntriesAsync(UserId(), query, ct);

    [HttpGet("journal/{id:guid}"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<JournalEntryDto> GetJournalEntry(Guid id, CancellationToken ct) => accounting.GetJournalEntryAsync(UserId(), id, ct);

    [HttpGet("trial-balance"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<TrialBalanceDto> TrialBalance([FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetTrialBalanceAsync(UserId(), asOfUtc, branchId, ct);

    [HttpGet("general-ledger"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<GeneralLedgerDto> GeneralLedger([FromQuery] GeneralLedgerQuery query, CancellationToken ct) =>
        accounting.GetGeneralLedgerAsync(UserId(), query, ct);

    [HttpGet("profit-loss"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<ProfitAndLossDto> ProfitAndLoss([FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetProfitAndLossAsync(UserId(), fromUtc, toUtc, branchId, ct);

    [HttpGet("balance-sheet"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<BalanceSheetDto> BalanceSheet([FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetBalanceSheetAsync(UserId(), asOfUtc, branchId, ct);

    [HttpGet("ar-aging"), HasPermission(PermissionCatalog.AccountsAgingReceivablesView)]
    public Task<ArAgingSummaryDto> ArAging([FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, [FromQuery] Guid? customerId, CancellationToken ct) =>
        accounting.GetArAgingSummaryAsync(UserId(), asOfUtc, branchId, customerId, ct);

    [HttpGet("ar-aging/detail"), HasPermission(PermissionCatalog.AccountsAgingReceivablesView)]
    public Task<ArAgingDetailDto> ArAgingDetail([FromQuery] Guid customerId, [FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetArAgingDetailAsync(UserId(), customerId, asOfUtc, branchId, ct);

    [HttpGet("ap-aging"), HasPermission(PermissionCatalog.AccountsAgingPayablesView)]
    public Task<ApAgingSummaryDto> ApAging([FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, [FromQuery] Guid? supplierId, CancellationToken ct) =>
        accounting.GetApAgingSummaryAsync(UserId(), asOfUtc, branchId, supplierId, ct);

    [HttpGet("ap-aging/detail"), HasPermission(PermissionCatalog.AccountsAgingPayablesView)]
    public Task<ApAgingDetailDto> ApAgingDetail([FromQuery] Guid supplierId, [FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetApAgingDetailAsync(UserId(), supplierId, asOfUtc, branchId, ct);

    [HttpPost("journal/{id:guid}/reverse"), HasPermission(PermissionCatalog.AccountsJournalReverse)]
    public Task<JournalEntryDto> ReverseJournal(Guid id, ReverseJournalEntryRequest request, CancellationToken ct) =>
        accounting.ReverseJournalEntryAsync(UserId(), id, request, ct);

    [HttpGet("cost-centers"), HasPermission(PermissionCatalog.AccountsCostCentersView)]
    public Task<IReadOnlyList<CostCenterDto>> ListCostCenters([FromQuery] bool includeInactive, CancellationToken ct) =>
        accounting.ListCostCentersAsync(UserId(), includeInactive, ct);

    [HttpPost("cost-centers"), HasPermission(PermissionCatalog.AccountsCostCentersManage)]
    public Task<CostCenterDto> CreateCostCenter(CostCenterRequest request, CancellationToken ct) =>
        accounting.CreateCostCenterAsync(UserId(), request, ct);

    [HttpPost("cost-centers/{id:guid}/activate"), HasPermission(PermissionCatalog.AccountsCostCentersManage)]
    public async Task<IActionResult> ActivateCostCenter(Guid id, CancellationToken ct) { await accounting.SetCostCenterActiveAsync(UserId(), id, true, ct); return NoContent(); }

    [HttpPost("cost-centers/{id:guid}/deactivate"), HasPermission(PermissionCatalog.AccountsCostCentersManage)]
    public async Task<IActionResult> DeactivateCostCenter(Guid id, CancellationToken ct) { await accounting.SetCostCenterActiveAsync(UserId(), id, false, ct); return NoContent(); }

    [HttpGet("trial-balance/movement"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<TrialBalanceMovementDto> TrialBalanceMovement([FromQuery] DateTime fromUtc, [FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, [FromQuery] bool includeZeroBalances, CancellationToken ct) =>
        accounting.GetTrialBalanceMovementAsync(UserId(), fromUtc, asOfUtc, branchId, includeZeroBalances, ct);

    [HttpGet("cash-book"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<CashBankBookDto> CashBook([FromQuery] CashBankBookQuery query, CancellationToken ct) => accounting.GetCashBookAsync(UserId(), query, ct);

    [HttpGet("bank-book"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<CashBankBookDto> BankBook([FromQuery] CashBankBookQuery query, CancellationToken ct) => accounting.GetBankBookAsync(UserId(), query, ct);

    [HttpGet("day-book"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<DayBookDto> DayBook([FromQuery] DayBookQuery query, CancellationToken ct) => accounting.GetDayBookAsync(UserId(), query, ct);

    [HttpGet("cash-flow"), HasPermission(PermissionCatalog.AccountsJournalView)]
    public Task<CashFlowStatementDto> CashFlow([FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetCashFlowStatementAsync(UserId(), fromUtc, toUtc, branchId, ct);

    [HttpGet("reconciliation/ar"), HasPermission(PermissionCatalog.AccountsReconciliationView)]
    public Task<ControlReconciliationDto> ArReconciliation([FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetArControlReconciliationAsync(UserId(), asOfUtc, branchId, ct);

    [HttpGet("reconciliation/ap"), HasPermission(PermissionCatalog.AccountsReconciliationView)]
    public Task<ControlReconciliationDto> ApReconciliation([FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetApControlReconciliationAsync(UserId(), asOfUtc, branchId, ct);

    [HttpGet("reconciliation/cash-bank"), HasPermission(PermissionCatalog.AccountsReconciliationView)]
    public Task<CashBankControlReconciliationDto> CashBankReconciliation([FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, CancellationToken ct) =>
        accounting.GetCashBankControlReconciliationAsync(UserId(), asOfUtc, branchId, ct);

    [HttpGet("reconciliation/inventory"), HasPermission(PermissionCatalog.AccountsReconciliationView)]
    public Task<InventoryReconciliationDto> InventoryReconciliation([FromQuery] DateTime asOfUtc, [FromQuery] Guid? branchId, [FromQuery] Guid? godownId, CancellationToken ct) =>
        accounting.GetInventoryReconciliationAsync(UserId(), asOfUtc, branchId, godownId, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
