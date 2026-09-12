using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.BankReconciliations;
using Pharmacy.Application.Services.Accounting.RecurringJournals;
using Pharmacy.Application.Services.Accounting.Vouchers;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Accounting.PartyAdjustments;
using Pharmacy.Application.Services.Finance;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Tests;

public sealed class Phase4PostgreSqlTests
{
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Seeded_accounting_roles_have_both_bank_reconciliation_permissions()
    {
        await using var db = Open();
        foreach (var name in new[] { RoleCatalog.Owner, RoleCatalog.Manager, RoleCatalog.Accountant })
        {
            var role = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == name);
            Assert.Contains(role.RolePermissions, x => x.Permission!.Code == PermissionCatalog.AccountsReconciliationView);
            Assert.Contains(role.RolePermissions, x => x.Permission!.Code == PermissionCatalog.AccountsReconciliationManage);
        }
        var seed = await SeedAsync(db);
        var service = new BankReconciliationService(new BankReconciliationRepository(db), TimeProvider.System);
        var reconciliation = await service.StartReconciliationAsync(seed.Actor.Id, new(seed.Financial.Id, new(2026, 1, 1), new(2026, 1, 31), 10000, 10000, null));
        Assert.Equal(reconciliation.Id, (await service.GetReconciliationAsync(seed.Actor.Id, reconciliation.Id)).Id);
        Assert.Equal(10000, reconciliation.BookBalance);
        Assert.Equal(10000, await new PartyAdjustmentRepository(db).GetFinancialAccountBalanceAsync(seed.Financial.Id));
        var control = await new AccountingRepository(db).GetCashBankControlReconciliationAsync(DateTime.UtcNow, seed.Branch.Id);
        Assert.Equal(10000, control.CashOperationalTotal);
        var opening = await db.FinancialLedgerEntries.SingleAsync(x => x.FinancialAccountId == seed.Financial.Id);
        await service.MatchLinesAsync(seed.Actor.Id, reconciliation.Id, new([opening.Id]));
        var finalized = await service.FinalizeReconciliationAsync(seed.Actor.Id, reconciliation.Id, new(null, false));
        Assert.Equal(BankReconciliationStatus.Finalized, finalized.Status);
        var locked = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"FinancialLedgerEntries\" SET \"BankReconciliationId\" = NULL, \"ReconciledAtUtc\" = NULL WHERE \"Id\" = {opening.Id}"));
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation, locked.SqlState);
        var immutable = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"FinancialLedgerEntries\" SET \"Amount\" = 9000 WHERE \"Id\" = {opening.Id}"));
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation, immutable.SqlState);
        await service.ReopenReconciliationAsync(seed.Actor.Id, reconciliation.Id, new("Corrected statement"));
        await service.UnmatchLinesAsync(seed.Actor.Id, reconciliation.Id, new([opening.Id]));
        Assert.Null(opening.BankReconciliationId);
    }

    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Concurrent_recurring_generators_commit_each_occurrence_once()
    {
        await using var db = Open();
        var seed = await SeedAsync(db);
        var service = new RecurringJournalService(new RecurringJournalRepository(db), TimeProvider.System);
        var template = await service.CreateTemplateAsync(seed.Actor.Id, Template(seed));
        async Task<GenerateDueRecurringJournalsResultDto> Generate()
        {
            await using var context = Open();
            return await new RecurringJournalService(new RecurringJournalRepository(context), TimeProvider.System)
                .GenerateDueEntriesAsync(seed.Actor.Id, new(new(2026, 3, 1)));
        }
        var results = await Task.WhenAll(Generate(), Generate());
        Assert.All(results, x => Assert.Empty(x.Failures));
        Assert.Equal(3, results.SelectMany(x => x.Generated).Count(x => x.TemplateId == template.Id));
        var occurrences = await db.RecurringJournalOccurrences.AsNoTracking().Where(x => x.RecurringJournalTemplateId == template.Id).ToListAsync();
        Assert.Equal(3, occurrences.Count);
        Assert.Equal(3, occurrences.Select(x => x.ScheduledDate).Distinct().Count());
        Assert.Equal(3, await db.JournalEntries.CountAsync(x => occurrences.Select(o => o.JournalEntryId).Contains(x.Id)));
        Assert.Equal(new DateOnly(2026, 4, 1), await db.RecurringJournalTemplates.AsNoTracking().Where(x => x.Id == template.Id).Select(x => x.NextRunDate).SingleAsync());
        await service.SetTemplateActiveAsync(seed.Actor.Id, template.Id, false);
    }

    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Recurring_failure_rolls_back_journals_occurrences_audits_and_schedule_then_retry_succeeds()
    {
        await using var db = Open();
        var seed = await SeedAsync(db);
        var service = new RecurringJournalService(new RecurringJournalRepository(db), TimeProvider.System);
        var year = 3000 + Random.Shared.Next(2000);
        var closed = new AccountingPeriod { FiscalYear = year, PeriodNumber = 2, Name = "Failure test", StartDate = new(year, 2, 1), EndDate = new(year, 2, 28), Status = AccountingPeriodStatus.Closed };
        db.AccountingPeriods.Add(closed);
        await db.SaveChangesAsync();
        var template = await service.CreateTemplateAsync(seed.Actor.Id, Template(seed) with { StartDate = new(year, 1, 1) });
        var failed = await service.GenerateDueEntriesAsync(seed.Actor.Id, new(new(year, 3, 1)));
        Assert.Contains(failed.Failures, x => x.TemplateId == template.Id);
        Assert.DoesNotContain(failed.Generated, x => x.TemplateId == template.Id);
        await using var verify = Open();
        Assert.False(await verify.RecurringJournalOccurrences.AnyAsync(x => x.RecurringJournalTemplateId == template.Id));
        Assert.False(await verify.JournalEntries.AnyAsync(x => x.Reference == template.Name));
        Assert.False(await verify.AuditLogs.AnyAsync(x => x.EntityId == template.Id && x.Action == "RecurringJournalGenerated"));
        Assert.Equal(new DateOnly(year, 1, 1), await verify.RecurringJournalTemplates.Where(x => x.Id == template.Id).Select(x => x.NextRunDate).SingleAsync());
        var period = await verify.AccountingPeriods.SingleAsync(x => x.Id == closed.Id);
        period.Status = AccountingPeriodStatus.Open;
        await verify.SaveChangesAsync();
        var retryService = new RecurringJournalService(new RecurringJournalRepository(verify), TimeProvider.System);
        var retry = await retryService.GenerateDueEntriesAsync(seed.Actor.Id, new(new(year, 3, 1)));
        Assert.DoesNotContain(retry.Failures, x => x.TemplateId == template.Id);
        Assert.Equal(3, retry.Generated.Count(x => x.TemplateId == template.Id));
        Assert.DoesNotContain((await retryService.GenerateDueEntriesAsync(seed.Actor.Id, new(new(year, 3, 1)))).Generated, x => x.TemplateId == template.Id);
        await retryService.SetTemplateActiveAsync(seed.Actor.Id, template.Id, false);
    }

    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Typed_voucher_posts_new_lines_and_reverses_without_false_concurrency()
    {
        await using var db = Open();
        var seed = await SeedAsync(db);
        var service = new VoucherService(new VoucherRepository(db), TimeProvider.System);
        var draft = await service.CreateDraftAsync(seed.Actor.Id, new(VoucherType.CashReceipt, DateTime.UtcNow, seed.Branch.Id, null, "Phase4 repository regression",
            ChartOfAccountId: seed.Accounts[1].Id, Amount: 125, FinancialAccountId: seed.Financial.Id));
        await service.PostAsync(seed.Actor.Id, draft.Id);
        var reversed = await service.ReverseAsync(seed.Actor.Id, draft.Id);
        Assert.Equal(2, await db.VoucherLines.CountAsync(x => x.VoucherId == draft.Id));
        Assert.Equal(2, await db.VoucherLines.CountAsync(x => x.VoucherId == reversed.Id));
        Assert.Equal(0, await db.FinancialLedgerEntries.Where(x => x.ReferenceId == draft.Id || x.ReferenceId == reversed.Id).SumAsync(x => x.Amount));
        await Assert.ThrowsAsync<Pharmacy.Application.Common.RequestValidationException>(() => service.ReverseAsync(seed.Actor.Id, draft.Id));
    }

    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Supplier_reversals_restore_GL_ledger_allocations_and_cash_on_real_database()
    {
        await using var db = Open();
        var seed = await SeedAsync(db);
        var supplier = new Supplier { Name = "Phase4 " + Guid.NewGuid(), NormalizedName = Guid.NewGuid().ToString("N").ToUpperInvariant() };
        var bill = new GoodsReceipt { BranchId = seed.Branch.Id, SupplierId = supplier.Id, GrnNumber = Guid.NewGuid().ToString("N"), ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow), Subtotal = 200, NetTotal = 200 };
        db.AddRange(supplier, bill);
        await db.SaveChangesAsync();
        var repository = new PartyAdjustmentRepository(db);
        var service = new PartyAdjustmentService(repository, new JournalPostingService(new AccountingRepository(db), TimeProvider.System), TimeProvider.System);
        foreach (var source in new[] { JournalSourceType.DebitNote, JournalSourceType.SupplierWriteOff, JournalSourceType.SupplierAdvance })
        {
            Guid id;
            if (source == JournalSourceType.DebitNote) id = (await service.CreateDebitNoteAsync(seed.Actor.Id, new(supplier.Id, seed.Branch.Id, 125, "Supplier credit", null, DateTime.UtcNow, bill.Id))).Id;
            else if (source == JournalSourceType.SupplierWriteOff) id = (await service.CreateSupplierWriteOffAsync(seed.Actor.Id, new(supplier.Id, seed.Branch.Id, 125, "Waived", null, DateTime.UtcNow, bill.Id))).Id;
            else id = (await service.RecordSupplierAdvanceAsync(seed.Actor.Id, new(supplier.Id, seed.Branch.Id, 125, seed.Financial.Id, DateTime.UtcNow, null, null))).Id;
            await service.ReverseSupplierAdjustmentAsync(seed.Actor.Id, source, id, new("Correction"));
            Assert.False(await db.SupplierPaymentAllocations.AnyAsync(x => x.GoodsReceiptId == bill.Id));
            Assert.Equal(200, Assert.Single(await repository.GetOpenPayablesAsync(supplier.Id, seed.Branch.Id)).Outstanding);
            var original = await repository.GetJournalForSourceAsync(source, id);
            var reversal = await db.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.ReversesJournalEntryId == original!.Id);
            foreach (var account in original!.Lines.Concat(reversal.Lines).GroupBy(x => x.ChartOfAccountId)) Assert.Equal(0, account.Sum(x => x.Debit - x.Credit));
            await Assert.ThrowsAsync<Pharmacy.Application.Common.RequestValidationException>(() => service.ReverseSupplierAdjustmentAsync(seed.Actor.Id, source, id, new("Retry")));
        }
        Assert.Equal(0, await db.SupplierLedgerEntries.Where(x => x.SupplierId == supplier.Id).SumAsync(x => x.Amount));
        Assert.Equal(10000, await db.FinancialLedgerEntries.Where(x => x.FinancialAccountId == seed.Financial.Id).SumAsync(x => x.Amount));
    }

    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Finance_reversal_metadata_guards_allow_compensation_and_keep_amounts_immutable()
    {
        await using var db = Open();
        var seed = await SeedAsync(db);
        var category = await db.ExpenseCategories.FirstAsync(x => x.IsActive);
        var center = new CostCenter { Code = Guid.NewGuid().ToString("N")[..12], NormalizedCode = Guid.NewGuid().ToString("N")[..12], Name = "Phase4 verification" };
        db.CostCenters.Add(center);
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db), new JournalPostingService(new AccountingRepository(db), TimeProvider.System), TimeProvider.System);
        var expense = await service.PostExpenseAsync(seed.Actor.Id, new(seed.Branch.Id, category.Id, seed.Financial.Id, DateTime.UtcNow, 125, "Phase4 expense", null, null, null, center.Id));
        var income = await service.PostOtherIncomeAsync(seed.Actor.Id, new(seed.Branch.Id, seed.Financial.Id, DateTime.UtcNow, 125, "Phase4 income", null, null, center.Id));
        Assert.Equal(center.Id, expense.CostCenterId);
        Assert.Equal(center.Id, income.CostCenterId);
        await service.ReverseExpenseAsync(seed.Actor.Id, expense.Id, new("Correction"));
        await service.ReverseOtherIncomeAsync(seed.Actor.Id, income.Id, new("Correction"));
        Assert.NotNull((await db.Expenses.SingleAsync(x => x.Id == expense.Id)).ReversedAtUtc);
        Assert.NotNull((await db.OtherIncomes.SingleAsync(x => x.Id == income.Id)).ReversedAtUtc);
        Assert.Equal(10000, await db.FinancialLedgerEntries.Where(x => x.FinancialAccountId == seed.Financial.Id).SumAsync(x => x.Amount));
        Assert.Equal(0, await db.JournalEntryLines.Where(x => x.CostCenterId == center.Id).SumAsync(x => x.Debit - x.Credit));
        var immutable = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Expenses\" SET \"Amount\"=150 WHERE \"Id\"={expense.Id}"));
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation, immutable.SqlState);
        await Assert.ThrowsAsync<Pharmacy.Application.Common.RequestValidationException>(() => service.ReverseExpenseAsync(seed.Actor.Id, expense.Id, new("Retry")));
        await Assert.ThrowsAsync<Pharmacy.Application.Common.RequestValidationException>(() => service.ReverseOtherIncomeAsync(seed.Actor.Id, income.Id, new("Retry")));
    }

    private static PharmacyDbContext Open() => new(new DbContextOptionsBuilder<PharmacyDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("PHARMACY_TEST_CONNECTION_STRING")!).Options);
    private sealed record Seed(Branch Branch, User Actor, FinancialAccount Financial, List<ChartOfAccount> Accounts);
    private static async Task<Seed> SeedAsync(PharmacyDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var branch = new Branch { Code = suffix[..12], NormalizedCode = suffix[..12].ToUpperInvariant(), Name = "Phase4 test " + suffix };
        var role = await db.Roles.SingleAsync(x => x.Name == RoleCatalog.Owner);
        var actor = new User { Username = suffix, NormalizedUsername = suffix.ToUpperInvariant(), FullName = "Phase4 test", PasswordHash = "not-a-real-hash", RoleId = role.Id, BranchId = branch.Id };
        var financial = new FinancialAccount { Name = "Till", NormalizedName = "TILL", BranchId = branch.Id, AccountType = FinancialAccountType.Cash, OpeningBalance = 10000 };
        db.AddRange(branch, actor, financial);
        await db.SaveChangesAsync();
        db.FinancialLedgerEntries.Add(new FinancialLedgerEntry { FinancialAccountId = financial.Id, BranchId = branch.Id,
            EntryType = FinancialLedgerEntryType.OpeningBalance, Amount = 10000, ReferenceType = "FinancialAccount", ReferenceId = financial.Id,
            Description = "Opening balance", CreatedByUserId = actor.Id, OccurredAtUtc = new(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        var accounts = await db.ChartOfAccounts.Where(x => x.IsActive && x.IsPostingAccount).OrderBy(x => x.Code).Take(2).ToListAsync();
        return new(branch, actor, financial, accounts);
    }
    private static RecurringJournalTemplateRequest Template(Seed seed) => new("Phase4 " + Guid.NewGuid().ToString("N"), null, RecurringJournalFrequency.Monthly,
        new(2026, 1, 1), null, seed.Branch.Id, [new(seed.Accounts[0].Id, 125, 0), new(seed.Accounts[1].Id, 0, 125)]);
}
