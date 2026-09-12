# Phase 6 Pricing and Business Automation Audit

## Starting point

Phase 5 checkpoint: `804bb979673ddaac2f8e1d6f011f2e4a8dcb7ef4`.

## Existing and reused

- `PriceLevel`, `ProductPriceLevel`, and `ProductPriceBreak` already resolve customer price levels, flat prices, and quantity breaks.
- Sales, quotations, and sales orders snapshot resolved prices and preserve manual override and below-cost controls.
- Product, ProductBatch, Inventory, StockMovement, godown, branch, purchase order, supplier, and Phase 5 MIS data are already available.
- Purchasing already supports draft purchase orders, updates, submission, receiving, and accounting-safe posting.
- Branch/godown access, permissions, audit logs, settings, and Flutter shell navigation are established.

## Original Phase 6 slice retained

- Additive `PricingRule` and `PricingPriceHistory` domain contracts with customer, product, category, manufacturer, price-level, sale-type, quantity, branch, and date scopes.
- Deterministic rule resolution: customer-specific, product-specific, price-level, category, manufacturer, quantity, and branch specificity are ranked first; explicit priority and creation time break remaining ties.
- Fixed-price, percentage-discount, and fixed-discount adjustments; promotions use the same safe resolver path.
- Resolved prices are rounded, nonnegative, and never below product cost.
- Optional resolver context preserves the existing no-rule fallback to price levels and product retail pricing.
- Margin-vs-markup pricing suggestions with explicit rounding and no automatic price mutation.
- Reorder suggestions using current stock, product/inventory reorder levels, 30-day sales velocity, pending purchase orders, last batch purchase, supplier, and days of stock.
- Live deduplicated business alerts for low stock, out of stock, near expiry, expired positive stock, and below-cost retail pricing.
- Safe automation rule storage and idempotent alert refresh/run metadata; automation only creates/refreshes alerts and never posts sales, payments, journals, stock, or prices.
- Permission-protected Phase 6 API endpoints and a Flutter Business Automation workspace with pricing, reorder, alerts, and automation tabs.
- One coherent EF migration with indexes, integrity constraints, permission seeds, and role grants.

## Completion of the agreed Phase 6 scope

The original slice above was retained. The remaining work adds:

- Bulk pricing by category, manufacturer, price level, or explicit products: increase/decrease percentage, margin, markup, and rounding. Immutable actor-bound previews expire after 15 minutes; explicit confirmation, below-cost guards, atomic compare-and-update, history, and old/new audits protect application.
- Reorder selection creates Draft purchase orders grouped by supplier, branch, and godown. Suggested quantities remain separate from editable final quantities and survive draft edits. Identical requests are suppressed; supplier assistance uses actual last-purchase evidence. Drafts are neither approved nor received automatically.
- Movement-derived slow/dead stock with configurable days and a minimum-value query filter; no permanent product classification. Reorder uses pending godown-specific purchases and allocates unscoped pending quantities once.
- Customer overdue, near/exceeded credit limit, and high outstanding alerts; supplier due, overdue, and high outstanding alerts. Existing aging calculations and source/rule/day keys are reused, with branch, godown, and financial permission controls.
- Six typed settings in existing SystemSettings: expiry days, slow days, dead days, minimum margin percentage, reorder cover days, and rounding increment.
- Ten reports through the existing Phase 5 engine: Price Change History, Promotion Performance, Margin Exceptions, Low Margin Products, Reorder Suggestions, Stockout Risk, Slow/Dead Stock Summary, Expiry Alert Summary, Automation Execution Log, and Alert Summary. Promotion results reuse sales/return allocations; cost fields retain existing permission protection.
- Flutter pricing rule and promotion create/edit/activation forms, priorities, named scopes, adjustment types/values, quantities, sale/customer types, date validation, and permission gating. Bulk preview, draft creation, alert filters, thresholds, suggestions, and automation results are exposed in the Phase 6 workspace.
- Concise POS/Wholesale source labels for retail, levels, quantity breaks, rules, promotions, overrides, and document snapshots. Customer/quantity changes re-resolve prices, while legacy retail fallback remains intact; source responses contain no cost fields.
- Material GRN cost-change suggestions show old/new cost, selling price, margin, and proposed price. Managers explicitly accept through the protected pricing/history path; purchasing never automatically changes prices.
- Near-expiry batch discount previews show final price, projected margin, and a below-cost guard. They are suggestions only.
- Safe automation supports CreateAlert, CreateDraftPurchaseOrder, and FlagForReview, using explicit supported numeric conditions, deterministic daily execution fingerprints, transaction locks, and duplicate suppression. It cannot execute scripts or post financial documents.
- An additive migration preserves the original Phase 6 migration and adds typed draft godown/suggested-quantity metadata.

Verification results are recorded in [PHASE6_CLOSURE_REPORT.md](PHASE6_CLOSURE_REPORT.md). No commit or Phase 7 work is included.

## Intentional deferrals

- Scheduled/background execution: manual execution remains the supported boundary until scheduler ownership and operational safeguards are justified.
- Supplier scoring beyond actual available purchase data: no invented rankings or reliability estimates.
- Advanced Buy-X/Get-Y promotions: requires separate allocation and return semantics.
- Autonomous purchasing: automation may create reviewable drafts only.
- Automatic selling-price changes: bulk changes and cost suggestions require explicit confirmation; expiry discounts remain previews.
- AI forecasting: current reorder calculations use actual stock, movement, and pending purchase data.
