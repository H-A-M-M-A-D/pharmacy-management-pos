using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.RecurringJournals;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/accounts/recurring-journals")]
public sealed class RecurringJournalsController(IRecurringJournalService recurring) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.AccountsRecurringView)]
    public Task<IReadOnlyList<RecurringJournalTemplateDto>> List([FromQuery] bool includeInactive, CancellationToken ct) => recurring.ListTemplatesAsync(UserId(), includeInactive, ct);

    [HttpPost, HasPermission(PermissionCatalog.AccountsRecurringManage)]
    public Task<RecurringJournalTemplateDto> Create(RecurringJournalTemplateRequest request, CancellationToken ct) => recurring.CreateTemplateAsync(UserId(), request, ct);

    [HttpPost("{id:guid}/activate"), HasPermission(PermissionCatalog.AccountsRecurringManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) { await recurring.SetTemplateActiveAsync(UserId(), id, true, ct); return NoContent(); }

    [HttpPost("{id:guid}/deactivate"), HasPermission(PermissionCatalog.AccountsRecurringManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) { await recurring.SetTemplateActiveAsync(UserId(), id, false, ct); return NoContent(); }

    [HttpPost("generate-due"), HasPermission(PermissionCatalog.AccountsRecurringManage)]
    public Task<GenerateDueRecurringJournalsResultDto> GenerateDue(GenerateDueRecurringJournalsRequest request, CancellationToken ct) => recurring.GenerateDueEntriesAsync(UserId(), request, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
