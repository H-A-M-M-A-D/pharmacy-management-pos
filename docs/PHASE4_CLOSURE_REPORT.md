# Accounting Phase 4 closure

Verification date: 2026-09-12. This report covers the current uncommitted accounting Phase 4 work. No commit was created, Phase 5 was not started, and no production database was accessed.

## Bank-reconciliation failures

The two failures were:

1. `Pharmacy.Tests.BankReconciliationServiceTests.Candidate_lines_are_scoped_to_the_statement_end_date_and_omitted_from_the_list_view`
2. `Pharmacy.Tests.BankReconciliationServiceTests.Candidate_lines_beyond_the_bound_are_truncated_but_the_total_count_reflects_every_match`

Both failed with `ForbiddenOperationException` in `BankReconciliationService.GetReconciliationAsync`, before their query assertions ran. Their fake Accountant held only `accounts.reconciliation.manage`; reading detail requires `accounts.reconciliation.view`.

Inspection confirmed that `BankReconciliationsController` and the service agree on separate view/manage permissions. `IdentityCatalog` contains both permission codes. `SeedPhase4Permissions` grants both to Owner, Manager, and Accountant. The production PostgreSQL repositories load role permissions through their Permission navigation. PostgreSQL tests verify those actual seeded role grants.

Classification for both: missing view grant in the fake fixture, not a changed endpoint permission, role-mapping mismatch, or absent production permission seed. Both fixtures now include both seeded grants. The two tests were rerun together independently and passed 2/2. Their assertions were preserved. Separate negative tests verify that manage alone does not authorize reads, view alone does not authorize mutations, and an Accountant cannot access another branch's reconciliation.

## Fixes completed

- Reconciliation lists are scoped by financial account and actor branch and bounded to 100 rows. Candidate queries use the correctly named `currentReconciliationId`, statement-end/account filters, deterministic ordering and a 500-row bound. Candidate counts and monetary totals cover all matching rows, including rows beyond the bound. List summaries do not materialize candidate lines. Reopening clears finalization balance snapshots so open detail can calculate current balances.
- Reconciliation, cash/bank control and supplier-advance balance queries now use posted ledger totals. Opening balances already have ledger entries and must not be added a second time.
- Recurring journals commit each template's due batch, occurrences, audit rows and NextRunDate in one transaction. A PostgreSQL row lock serializes competing generators. Failed batches roll back completely and clear tracking, including entries accepted by earlier SaveChanges calls inside the rolled-back transaction. Retry cannot duplicate occurrences. Cancellation also clears tracking and propagates. Accountant list/generation/template management is branch-scoped.
- Typed voucher posting explicitly inserts newly appended draft lines. Dedicated tests exercise the real VoucherRepository line-staging path and model guards; a PostgreSQL regression verifies posting and reversal without false concurrency failures.
- Supplier debit notes and write-offs now have a dedicated reversal API that compensates the permanent supplier ledger and GL, releases current invoice settlement links, and preserves released-allocation details in immutable audit history. Unapplied supplier advances can be reversed with compensating cash/GL entries. Reversed advances report zero available balance and cannot be applied. Duplicate reversals are rejected without duplicate postings.
- Expense and other-income reversal journals retain their CostCenter attribution.
- PostgreSQL metadata guards now permit reconciliation matching and one-time expense/income reversal markers while retaining immutable financial amounts and finalized-match locking. The real PostgreSQL workflows test these guards, including direct-SQL rejection of financial edits and finalized-line changes.
- Flutter now exposes Budgets and Party Adjustments, and its Accounts shell gate recognizes Phase 4 view permissions. The journal-detail footer wraps within the available width. Its test now checks the updated text and absence of the unauthorized reversal action.

## Migration verification

`20260912040000_AddFinanceCostCenters` appears in EF migrations list. Its missing target-model metadata was added and verified by a dedicated migration-model test.

The complete migration chain applied successfully to a new `pharmacy_phase4_fresh` database. Schema inspection on that database, `pharmacy_test`, and `pharmacy_dev` confirms:

- `Expenses.CostCenterId` and `OtherIncomes.CostCenterId`: nullable UUID columns.
- `IX_Expenses_CostCenterId` and `IX_OtherIncomes_CostCenterId`: present.
- Both CostCenter FKs: `ON DELETE RESTRICT`.
- Neither table retains `ReversalJournalEntryId`.

The two unused `ReversalJournalEntryId` columns were removed with their indexes/FKs by `20260912063859_RemoveUnusedFinanceReversalLinks`. This migration also brings the unique voucher-reversal index into agreement with the model. Reversal identity remains implemented through posted journal source identity and the existing journal/voucher reversal links.

The latest migration is `20260912070538_AlignPhase4FinanceMetadataGuards`. It is applied to all three verification databases. A second `pharmacy_dev` database update reports that the database is already up to date. EF migrations list contains no pending marker. The snapshot and latest target model agree with the model: `dotnet ef migrations has-pending-model-changes` reports `No changes have been made to the model since the last migration.`

SQL/schema and EF verification output is retained in `artifacts/phase4-closure/`.

## Test coverage and results

VoucherService has 13 dedicated test cases covering cash receipt, cash payment, bank receipt, bank payment, contra, GL reversal, financial-ledger reversal, duplicate reversal, closed-period rejection and soft-close override. The period cases use the real DbContext posting guard. Supplier adjustment tests cover write-off FIFO allocation, debit-note settlement of supplier credits, supplier advances and their applications, supplier-ledger/GL posting, reversal and duplicate rejection. PostgreSQL tests verify supplier reversals across GL, ledger, allocations and cash.

Flutter has 21 additional widget tests. All thirteen requested screens are reachable and load API responses: Accounting Periods, Bank Reconciliation, Cash Book, Bank Book, Day Book, Cash Flow, AR/AP/Inventory/Cash-Bank Reconciliation, Recurring Journals, Budgets and Party Adjustments. Action tests exercise close/reopen period, finalize/reopen reconciliation, budget create/edit, party-adjustment validation/posting and recurring generation with refreshed schedule/result. Permission tests cover navigation and mutation controls.

| Verification | Result |
| --- | --- |
| dotnet build | Passed; 0 warnings, 0 errors |
| Full in-memory suite | 292 passed, 0 failed, 0 skipped |
| Full PostgreSQL suite | 55 passed, 0 failed, 0 skipped |
| Combined backend suite | 347 passed, 0 failed, 0 skipped |
| EF pending-model check | No changes since the last migration |
| flutter pub get | Passed |
| flutter analyze | No issues found |
| flutter test | 118 passed, 0 failed |
| flutter build windows --release | Passed |
| git diff --check | Passed |
| Untracked source whitespace | Clean |
| git status | Phase 4 changes remain modified/untracked; uncommitted |

The Windows executable is `desktop/pharmacy_pos/build/windows/x64/runner/Release/pharmacy_pos.exe`. Backend test results are retained under `backend/Pharmacy.Tests/TestResults/phase4-final-*.trx`.

## Operational limits

Recurring generation remains a manual action with a maximum of 60 occurrences per template per invocation. Reconciliation detail displays at most 500 candidate lines and lists at most 100 reconciliations; totals/counts are not truncated. Applied supplier advances cannot be automatically reversed because their existing invoice settlements require a separate correction. Supplier credits reducing AP are represented by supplier debit notes. The fresh verification database is retained for inspection. Flutter action tests use API fixtures; the corresponding accounting persistence workflows are covered separately on PostgreSQL.

Phase 4 is complete against the requested closure checks. All separate and combined backend suites and Flutter checks are green. There are no ignored or reclassified failures. All changes remain uncommitted; Phase 5 has not started.
