using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.CashierShifts;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.CashierShifts;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class CashierShiftManagementTests
{
    [Fact]
    public async Task Open_shift_succeeds_then_blocks_a_second_concurrent_open_shift()
    {
        var f = new Fixture(PermissionCatalog.CashierShiftOpen);
        var opened = await f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, 500, "Till 1", "start of day"));
        Assert.Equal(CashierShiftStatus.Open, opened.Status);
        Assert.Equal(500, opened.OpeningCash);

        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, 100, null, null)));
    }

    [Fact]
    public async Task Open_shift_rejects_negative_opening_cash()
    {
        var f = new Fixture(PermissionCatalog.CashierShiftOpen);
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, -1, null, null)));
    }

    [Fact]
    public async Task Close_shift_computes_expected_cash_from_sales_refunds_receipts_payouts_and_drawer_entries()
    {
        var f = new Fixture(PermissionCatalog.CashierShiftOpen, PermissionCatalog.CashierShiftClose, PermissionCatalog.CashierShiftDrawerAdjust);
        var opened = await f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, 1000, null, null));
        await f.Service.AddDrawerEntryAsync(f.Actor.Id, opened.Id, new(CashierShiftDrawerEntryType.CashIn, 200, "extra float"));
        await f.Service.AddDrawerEntryAsync(f.Actor.Id, opened.Id, new(CashierShiftDrawerEntryType.CashOut, 50, "bank deposit"));

        f.Figures = new ShiftCashFigures(
            new Dictionary<SalePaymentMethod, (decimal Sales, decimal Refunds)>
            {
                [SalePaymentMethod.Cash] = (3000, 300),
                [SalePaymentMethod.Card] = (1200, 0)
            },
            CustomerCashReceived: 400,
            CashPaidOut: 150);

        // Expected = 1000 opening + 3000 cash sales + 400 customer cash + 200 manual in
        //          - 300 cash refunds - 150 cash paid out - 50 manual out = 4100
        var closed = await f.Service.CloseShiftAsync(f.Actor.Id, opened.Id, new(4100, "counted twice"));
        Assert.Equal(CashierShiftStatus.Closed, closed.Status);
        Assert.Equal(4100, closed.ExpectedCash);
        Assert.Equal(4100, closed.ActualCountedCash);
        Assert.Equal(0, closed.CashVariance);
        Assert.Equal(4200, closed.TotalSales);
        Assert.Equal(300, closed.TotalRefunds);
        Assert.Contains(closed.PaymentBreakdown, x => x.PaymentMethod == SalePaymentMethod.Card && x.SalesAmount == 1200);

        Assert.Equal(2, f.Journal.Posted.Count);
        var cashIn = f.Journal.Posted[0];
        Assert.Equal(JournalSourceType.CashierDrawerEntry, cashIn.SourceType);
        Assert.Equal(200, cashIn.Lines.Single(x => x.Account == AccountMappingKey.Cash).Debit);
        Assert.Equal(200, cashIn.Lines.Single(x => x.Account == AccountMappingKey.DrawerClearing).Credit);
        var cashOut = f.Journal.Posted[1];
        Assert.Equal(50, cashOut.Lines.Single(x => x.Account == AccountMappingKey.DrawerClearing).Debit);
        Assert.Equal(50, cashOut.Lines.Single(x => x.Account == AccountMappingKey.Cash).Credit);
    }

    [Theory]
    [InlineData(85, 15, true)]
    [InlineData(120, 20, false)]
    public async Task Reconciliation_posts_exact_shortage_or_excess(decimal actualCash, decimal varianceAmount, bool shortage)
    {
        var f = new Fixture(PermissionCatalog.CashierShiftOpen, PermissionCatalog.CashierShiftClose, PermissionCatalog.CashierShiftReconcile);
        var opened = await f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, 100, null, null));
        f.Figures = EmptyFigures();
        await f.Service.CloseShiftAsync(f.Actor.Id, opened.Id, new(actualCash, null));
        Assert.Empty(f.Journal.Posted);

        await f.Service.ReconcileShiftAsync(f.Actor.Id, opened.Id, new("manager approved variance"));

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.CashierShiftVariance, journal.SourceType);
        Assert.Equal(opened.Id, journal.SourceId);
        var cash = journal.Lines.Single(x => x.Account == AccountMappingKey.Cash);
        var overShort = journal.Lines.Single(x => x.Account == AccountMappingKey.CashOverShort);
        Assert.Equal(shortage ? 0 : varianceAmount, cash.Debit);
        Assert.Equal(shortage ? varianceAmount : 0, cash.Credit);
        Assert.Equal(shortage ? varianceAmount : 0, overShort.Debit);
        Assert.Equal(shortage ? 0 : varianceAmount, overShort.Credit);
    }

    [Fact]
    public async Task Closed_shift_cannot_be_closed_again()
    {
        var f = new Fixture(PermissionCatalog.CashierShiftOpen, PermissionCatalog.CashierShiftClose);
        var opened = await f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, 100, null, null));
        f.Figures = EmptyFigures();
        await f.Service.CloseShiftAsync(f.Actor.Id, opened.Id, new(100, null));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            f.Service.CloseShiftAsync(f.Actor.Id, opened.Id, new(100, null)));
    }

    [Fact]
    public async Task Only_own_cashier_or_close_any_holder_can_close_or_add_drawer_entries()
    {
        var f = new Fixture(PermissionCatalog.CashierShiftOpen, PermissionCatalog.CashierShiftClose, PermissionCatalog.CashierShiftDrawerAdjust);
        var opened = await f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, 100, null, null));

        var otherRole = new Role { Name = "Cashier" };
        otherRole.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = PermissionCatalog.CashierShiftClose, Description = "x", Category = "test" } });
        otherRole.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = PermissionCatalog.CashierShiftDrawerAdjust, Description = "x", Category = "test" } });
        var otherCashier = new User { Username = "other", NormalizedUsername = "OTHER", FullName = "Other Cashier", PasswordHash = "hash", BranchId = f.Branch.Id, RoleId = otherRole.Id, Role = otherRole };
        f.Actors.Add(otherCashier);

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            f.Service.AddDrawerEntryAsync(otherCashier.Id, opened.Id, new(CashierShiftDrawerEntryType.CashIn, 50, "not yours")));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            f.Service.CloseShiftAsync(otherCashier.Id, opened.Id, new(100, null)));

        f.ManagerRole.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = PermissionCatalog.CashierShiftCloseAny, Description = "x", Category = "test" } });
        f.Manager.RoleId = f.ManagerRole.Id;
        f.Manager.Role = f.ManagerRole;
        f.Figures = EmptyFigures();
        var closed = await f.Service.CloseShiftAsync(f.Manager.Id, opened.Id, new(100, "manager closed on cashier's behalf"));
        Assert.Equal(CashierShiftStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task Reconcile_requires_closed_status_and_reconcile_permission_then_blocks_repeat_reconciliation()
    {
        var f = new Fixture(PermissionCatalog.CashierShiftOpen, PermissionCatalog.CashierShiftClose, PermissionCatalog.CashierShiftReconcile);
        var opened = await f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, 100, null, null));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReconcileShiftAsync(f.Actor.Id, opened.Id, new(null)));

        f.Figures = EmptyFigures();
        await f.Service.CloseShiftAsync(f.Actor.Id, opened.Id, new(100, null));
        var reconciled = await f.Service.ReconcileShiftAsync(f.Actor.Id, opened.Id, new("reviewed, matches"));
        Assert.Equal(CashierShiftStatus.Reconciled, reconciled.Status);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReconcileShiftAsync(f.Actor.Id, opened.Id, new(null)));
    }

    [Fact]
    public async Task Missing_permission_is_forbidden()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            f.Service.OpenShiftAsync(f.Actor.Id, new(f.Branch.Id, 100, null, null)));
    }

    private static ShiftCashFigures EmptyFigures() => new(new Dictionary<SalePaymentMethod, (decimal Sales, decimal Refunds)>(), 0, 0);

    private sealed class Fixture : ICashierShiftRepository
    {
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main" };
        public readonly List<User> Actors = [];
        public readonly List<CashierShift> Shifts = [];
        public readonly List<AuditLog> Audits = [];
        public readonly Role ManagerRole = new() { Name = RoleCatalog.Manager };
        public readonly RecordingJournalPostingService Journal = new();
        public User Actor { get; }
        public User Manager { get; }
        public CashierShiftService Service { get; }
        public ShiftCashFigures Figures = EmptyFigures();

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Cashier };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "cashier", NormalizedUsername = "CASHIER", FullName = "Cashier One", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            Manager = new User { Username = "manager", NormalizedUsername = "MANAGER", FullName = "Manager", PasswordHash = "hash", BranchId = Branch.Id, RoleId = ManagerRole.Id, Role = ManagerRole };
            Actors.Add(Actor);
            Actors.Add(Manager);
            Service = new(this, Journal, TimeProvider.System);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(Actors.FirstOrDefault(x => x.Id == actorId));
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<CashierShift?> GetOpenShiftForCashierAsync(Guid cashierUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Shifts.FirstOrDefault(x => x.CashierUserId == cashierUserId && x.Status == CashierShiftStatus.Open));
        public Task<CashierShift?> GetShiftAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Shifts.FirstOrDefault(x => x.Id == id));
        public Task AddShiftAsync(CashierShift shift, CancellationToken cancellationToken = default) { Shifts.Add(shift); return Task.CompletedTask; }
        public Task AddDrawerEntryAsync(CashierShiftDrawerEntry entry, CancellationToken cancellationToken = default)
        {
            Shifts.Single(x => x.Id == entry.CashierShiftId).DrawerEntries.Add(entry);
            return Task.CompletedTask;
        }
        public Task AddPaymentSummaryAsync(CashierShiftPaymentSummary summary, CancellationToken cancellationToken = default)
        {
            Shifts.Single(x => x.Id == summary.CashierShiftId).PaymentSummaries.Add(summary);
            return Task.CompletedTask;
        }
        public Task<ShiftCashFigures> ComputeCashFiguresAsync(Guid branchId, Guid cashierUserId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) => Task.FromResult(Figures);
        public Task<PagedResult<CashierShiftListItemDto>> ListShiftsAsync(CashierShiftListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<CashierShiftListItemDto>([], 1, 25, 0));
        public Task<IReadOnlyList<CashierShift>> ListShiftsForDateAsync(Guid branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CashierShift>>(Shifts.Where(x => x.BranchId == branchId && x.ClosedAtUtc >= fromUtc && x.ClosedAtUtc < toUtc).ToList());
        public Task<int> CountOpenShiftsAsync(Guid branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) => Task.FromResult(Shifts.Count(x => x.BranchId == branchId && x.Status == CashierShiftStatus.Open));
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingJournalPostingService : IJournalPostingService
    {
        public readonly List<JournalPostingRequest> Posted = [];
        public Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default)
        {
            Posted.Add(request);
            return Task.CompletedTask;
        }
    }
}
