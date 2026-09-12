# Final Acceptance Test Matrix

Manual acceptance tests to run against a staging environment (real PostgreSQL, real Windows client, real printer/scanner where available) before accepting a release for production or pilot use. These complement, not replace, the automated backend/Flutter test suites — automated tests prove the code behaves correctly against its own assumptions; this matrix proves the assembled system behaves correctly for a real operator.

Run in the order listed where a later test's prerequisites depend on an earlier one's output (noted per test). Record actual result, tester, and date for each row when executing.

## 1. Login and security

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 1.1 | A deactivated user account exists | Attempt login with its credentials | Login rejected with a clear message; no token issued |
| 1.2 | An active user | Enter the wrong password 5 times (or the configured `AuthenticationSecurity:MaximumFailedAttempts`) | Account locks out; further correct-password attempts are also rejected until the lockout window (`LockoutMinutes`) elapses |
| 1.3 | A locked-out user from 1.2 | Wait for the lockout window to pass, then log in with the correct password | Login succeeds and failed-attempt counter resets |
| 1.4 | A user flagged `mustChangePassword` (e.g. freshly created) | Log in | Client redirects to a forced password-change screen before any other screen is reachable |
| 1.5 | An active user | Log in, then use "Sign out everywhere" (or an admin resets the user's role/branch) | Existing token(s) stop working on the next request; user must log in again |
| 1.6 | Backend stopped or unreachable | Attempt login | Client shows "cannot connect to the server" — never "wrong password" |
| 1.7 | Backend reachable, database stopped | Attempt login | `/api/health/ready` reports unhealthy; client shows a server/database-unavailable message, not a generic error |
| 1.8 | Logged in as a role lacking a specific permission (e.g. Cashier trying Administration) | Attempt to open a screen/action gated by that permission | Navigation hides or disables the action; if reached directly, the server returns 403 with a readable message, not a stack trace |

## 2. POS cash sale

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 2.1 | A cashier is logged in; at least one active product has available stock in the cashier's branch/godown | Scan/search the product's barcode, confirm quantity, complete payment with exact cash | Sale posts; invoice number assigned; stock decremented by the sold quantity; receipt shown |
| 2.2 | Same as above | Repeat with cash tendered greater than the total | Change due is computed and displayed correctly |
| 2.3 | A product with zero available stock | Scan its barcode | Clear "out of stock" message; item is not added to the cart |
| 2.4 | An unrecognized barcode | Scan it | Clear "no match" message; cart unchanged |
| 2.5 | A product available in two batches with different expiry dates | Sell a quantity spanning both batches | FEFO allocates from the earlier-expiring batch first; receipt/allocation reflects both batches |
| 2.6 | A completed sale from 2.1 | Double-click "Complete Sale" rapidly, or resubmit the identical request | Only one sale is posted; the duplicate attempt is rejected with a clear "already submitted" message, not a second invoice |

## 3. Credit sale

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 3.1 | A cashier with `sales.credit`; an active customer with `CreditAllowed=true` and available credit limit | Post a sale on full credit for that customer | Sale posts; customer ledger shows a new receivable entry for the full amount |
| 3.2 | A customer already at their credit limit | Attempt a credit sale | Rejected with a credit-limit-exceeded message, unless the actor holds `sales.credit_limit_override` and supplies a reason |
| 3.3 | A customer with `CreditAllowed=false` | Attempt any credit portion | Rejected regardless of available numeric limit |
| 3.4 | A part-cash, part-credit sale | Post it | `AmountPaid` + `CreditAmount` equals the net total; ledger entry equals only the credit portion |

## 4. Wholesale sale

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 4.1 | A user with `sales.wholesale`; a customer assigned a non-default price level with a configured product price | Open Wholesale, add the product, confirm the resolved price | Price matches the assigned price level, not the retail default; source label indicates the price level |
| 4.2 | A quantity price break configured for a product | Add a quantity crossing the break threshold | Resolved price uses the break price and reflects the change live as quantity changes |
| 4.3 | A user without `sales.wholesale` | Attempt to post a wholesale-type sale (including by manipulating the request) | Rejected server-side even if attempted directly against the API |

## 5. Sale return / refund

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 5.1 | A posted cash sale | Return one full line, choose cash refund | Return posts; stock restored to the original batch; refund payment recorded equal to the line's refund amount |
| 5.2 | A posted sale with a damaged/expired disposition available | Return with "not resellable" disposition | Stock is not restored to sellable inventory; a damaged/expired movement is recorded instead |
| 5.3 | A posted credit sale with an open receivable | Return a line | Customer receivable reduces first; only the remaining cash portion requires a refund payment |
| 5.4 | A return already fully processed for a line | Attempt to return the same line again | Rejected — cannot exceed the original sold/allocated quantity |

## 6. Purchase / GRN (goods receipt)

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 6.1 | A user with `purchases.receive`; an active supplier and product | Post a direct purchase (paid quantity + bonus quantity, with discount/tax) | Batch created/reused; stock increases by paid + bonus quantity; supplier ledger payable increases by the net paid-quantity value only (bonus doesn't add payable) |
| 6.2 | A submitted purchase order | Receive against it, confirming quantities/prices match the order | Order's received quantity updates; receipt links to the order |
| 6.3 | A supplier invoice number already recorded for that supplier | Attempt to post another receipt with the same invoice number | Rejected as a duplicate |
| 6.4 | A completed receipt | Resubmit the identical goods-receipt request (double-click) | Only one receipt posts; the duplicate is rejected, not silently doubled |

## 7. Purchase return

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 7.1 | A posted goods receipt | Return part of one line's paid quantity | Stock reduces from the original batch; supplier ledger credit recorded for the returned value; remaining returnable quantity reduces accordingly |
| 7.2 | A goods receipt already fully returned | Attempt to return more | Rejected — cannot exceed original receipt quantity net of prior returns |

## 8. Customer payment

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 8.1 | A customer with an outstanding receivable | Record a payment less than the outstanding balance | Ledger shows a negative (payment) entry; outstanding balance reduces by the paid amount; oldest open invoices settle first (FIFO allocation) |
| 8.2 | A customer with multiple open invoices | Record a payment covering the oldest fully plus part of the next | Allocation splits correctly across the two invoices |

## 9. Supplier payment

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 9.1 | A supplier with an outstanding payable | Record a payment | Ledger shows a negative entry; payable reduces; allocation applies FIFO against open receipts |

## 10. Expenses

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 10.1 | A user with `expenses.create`/`expenses.post`; an active financial (cash/bank) account with sufficient balance | Post an expense against that account | Account balance reduces by the expense amount; a journal entry is posted (Dr expense, Cr cash/bank) |
| 10.2 | A financial account with balance lower than the expense amount | Attempt to post the expense from that account | Rejected — cannot take a cash/bank account negative |

## 11. Transfers (inter-godown/branch)

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 11.1 | A user with transfer permissions; stock in a source godown | Create → request → approve → dispatch a transfer | Source batch/stock decreases at dispatch; a `TransferOut` movement is recorded; destination stock is untouched until receipt |
| 11.2 | A dispatched transfer | Receive it fully at the destination | Destination stock increases; a `TransferIn` movement is recorded; transfer status becomes Received |
| 11.3 | A dispatched transfer | Receive only part of the dispatched quantity | Status becomes Partially Received; remaining quantity still receivable in a later receipt |
| 11.4 | A dispatched, never-fully-received transfer with a genuine shortfall | Resolve the discrepancy | A write-off journal entry posts (Dr loss, Cr inventory); no further stock movement is created (physical quantity is already correct) |

## 12. Stock count

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 12.1 | A user with `inventory.stock_count`; existing stock in a branch | Start a stock count session (full or scoped), enter counted quantities, finalize | Variance quantities post as adjustment movements; stock reflects counted quantities; session cannot be finalized twice |
| 12.2 | A finalized session | Attempt to finalize it again | Rejected |

## 13. Reports

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 13.1 | A user with `reports.sales` only (no cost/financial permission) | Open a sales report with cost/margin columns | Cost/margin/profit fields are absent from the response, not zeroed or hidden client-side only |
| 13.2 | A user with `reports.financial` | Open the same report | Cost/financial fields are present |
| 13.3 | Any report screen | Export to CSV | File opens correctly in a spreadsheet application, correct columns/encoding, no unauthorized cost fields for a restricted user |
| 13.4 | A non-Owner/Manager user assigned to one godown only | Open an inventory or sales report | Only that user's assigned godown's data appears |

## 14. Accounting

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 14.1 | A user with `accounts.voucher.create`/`.post` | Create and post a manual cash receipt voucher | A balanced journal entry posts; the linked financial account balance updates |
| 14.2 | A posted voucher | Reverse it | A compensating journal entry and financial ledger entry post; net effect on the account is zero; reversing twice is rejected |
| 14.3 | An accounting period closed for the voucher's date | Attempt to post a voucher/journal dated inside it | Rejected, unless the actor holds `accounts.post_to_soft_closed` and the period is soft-closed (not hard-closed) |
| 14.4 | Any journal entry, posted normally | Inspect debit/credit totals | Always equal; attempting to edit a posted entry directly is rejected by the database, not just the API |

## 15. Backup / restore

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 15.1 | A user with `system.backup` | Trigger "Create backup" | A `.backup` file appears in the configured backup directory; the backup record shows Completed with a size and timestamp |
| 15.2 | `pg_dump`/`pg_restore` not on PATH or misconfigured | Trigger a backup | Fails with a clear error; no partial/corrupt file left in the backup directory |
| 15.3 | A completed backup file | Follow `docs/DISASTER_RECOVERY.md`'s restore-validation procedure into an isolated database | Restore succeeds; row counts and latest migration match the source |
| 15.4 | More completed backups exist than the configured retention count | Trigger one more backup | Oldest backups beyond the retention count are removed from disk and marked accordingly; the most recent backups are never deleted |

## 16. Permissions

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 16.1 | Users in each of the 7 seeded roles | For each role, attempt one action known to be outside its grants (see `docs/PERMISSION_MATRIX.md`) | Rejected server-side (403), regardless of what the UI shows |
| 16.2 | A Cashier user | Attempt to read another branch's data by passing a different `branchId` directly to an API call | Rejected — branch scoping is enforced server-side |
| 16.3 | A user assigned to one godown only | Attempt to dispatch/receive a transfer for a godown they are not assigned to | Rejected |

## 17. Pricing rules

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 17.1 | A user with `pricing.manage` | Create a pricing rule scoped to a category with a percentage discount | Matching products at POS/Wholesale resolve to the discounted price; source label reflects the rule |
| 17.2 | Two overlapping rules with different priorities | Sell a product matching both | The higher-priority (or more specific) rule wins deterministically |
| 17.3 | A rule that would resolve below product cost | Attempt to sell at that price | Blocked unless the actor holds `sales.sell_below_cost` |

## 18. Bulk pricing

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 18.1 | A user with `pricing.bulk_update`; several products in a category | Preview a bulk percentage increase, then apply | Preview shows old/new prices without changing anything; apply updates only the previewed products, recorded in price history with actor/reason |
| 18.2 | A stale preview (older than ~15 minutes) | Attempt to apply it | Rejected — must re-preview |
| 18.3 | The same preview applied twice concurrently (two browser tabs/users) | Apply from both | Exactly one application succeeds; the other reports already-applied, not double-applied prices |

## 19. Reorder / draft PO

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 19.1 | A user with `inventory.reorder.create_po`; products below reorder level | Review reorder suggestions, select some, create draft purchase orders | Draft POs created, grouped by supplier/branch/godown; suggested vs. final quantity both retained; POs are Draft, not submitted/received |
| 19.2 | An identical reorder request submitted twice the same day | Submit it again | Second submission is suppressed as a duplicate, not a second set of draft POs |

## 20. Alerts

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 20.1 | Stock below reorder level, or a batch near expiry, or a customer near/over credit limit | Open the Alerts view | Corresponding alert appears, deduplicated (not repeated every refresh) |
| 20.2 | A user without `alerts.manage` | Attempt to dismiss/refresh alerts | Rejected/hidden as appropriate |

## 21. Automation

| # | Prerequisites | Steps | Expected result |
|---|---|---|---|
| 21.1 | A user with `automation.manage`/`automation.run`; a configured automation rule (e.g. low-stock → draft PO) | Run automation manually | Only the safe supported actions occur (create alert / create draft PO / flag for review); no sale, payment, journal, or price is posted automatically |
| 21.2 | Automation already run once today for a given rule | Run it again the same day | Deterministic daily fingerprint suppresses a duplicate run |

---

**Sign-off**: record tester name, date, environment (branch/database), and pass/fail with notes for every row before accepting a release build for production or pilot deployment. Any failed row blocks release until fixed and re-tested.
