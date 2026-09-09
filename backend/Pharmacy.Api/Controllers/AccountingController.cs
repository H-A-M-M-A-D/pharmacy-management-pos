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

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
