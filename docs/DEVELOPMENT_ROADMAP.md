# Pharmacy Management System - Development Roadmap

## Phase Overview

The project is structured in 18 distinct phases, prioritized by business value and dependency order.

---

## Phase 1: ✅ Foundation (COMPLETED)

**Duration**: 1 sprint
**Status**: COMPLETE

### Completed
- [x] Project structure with clean architecture
- [x] All core domain entities
  - Branch, User, Role, Permission, RolePermission
  - Product, ProductCategory, Manufacturer, Supplier
  - ProductBatch, Inventory, StockMovement, AuditLog
- [x] PostgreSQL DbContext with EF Core
- [x] Database migrations
- [x] Flutter Windows project structure
- [x] Basic login screen UI
- [x] Basic dashboard UI

### Deliverables
- Complete domain model with 13 core entities
- Initial database migration
- Flutter app shell with login/dashboard screens
- Architecture documentation

### Next Action
→ Proceed to Phase 2: Authentication

---

## Phase 2: Authentication & Authorization (COMPLETED)

**Duration**: 1 sprint
**Depends On**: Phase 1

### Tasks
1. **JWT Token Service**
   - Token generation with claims
   - Token validation and refresh
   - Expiry handling
   - SecurityKey management

2. **User Management API**
   - User CRUD operations
   - Password change endpoint
   - Last login tracking
   - Active/inactive toggle

3. **Role & Permission Seeding**
   - Seed 7 initial roles (Owner, Manager, Pharmacist, Cashier, PurchaseManager, Accountant, StoreKeeper)
   - Seed all permission codes and descriptions
   - Assign permissions to roles
   - Database seeding script

4. **Login Flow**
   - Backend: LoginAsync service complete
   - API: /login, /verify, /setup-owner endpoints tested
   - Frontend: Connect login screen to API
   - Token storage in Flutter
   - Automatic token refresh
   - Logout functionality

5. **Authorization Middleware**
   - Permission checking attribute
   - Role checking attribute
   - Branch isolation middleware
   - Unauthorized response handling

6. **Tests**
   - Auth flow tests (happy path + errors)
   - Permission checking tests
   - Token generation/validation tests
   - Password hashing/verification tests

### Deliverables
- Complete auth service
- Full set of auth API endpoints
- Flutter API client with auth
- Integration tests for auth flow
- Seeded database with roles/permissions

---

## Phase 3: Product Master Management (COMPLETED)

**Duration**: 1.5 sprints
**Depends On**: Phase 2

### Tasks
1. **Product API Endpoints**
   - GET /api/products (with pagination, filtering, sorting)
   - GET /api/products/{id}
   - POST /api/products (create)
   - PUT /api/products/{id} (update)
   - POST activate/deactivate endpoints (no destructive delete)
   - GET /api/products/search (SKU, barcode, name)

2. **ProductCategory API**
   - CRUD operations
   - Validation (unique names)
   - Category list endpoint

3. **Manufacturer API**
   - CRUD operations
   - Manufacturer list/search

4. **Product Validation**
   - SKU must be unique
   - Barcode must be unique (when present)
   - Required fields validation
   - Price validation (positive, decimal precision)
   - Reorder level validation

5. **Product Service Layer**
   - Search/filter implementation
   - Pagination support
   - DTOs for request/response
   - Mapping (Domain → DTO)

6. **Flutter UI**
   - Product list screen
   - Product detail screen
   - Product create/edit form
   - Category selector
   - Manufacturer selector
   - Search/filter interface

7. **Tests**
   - Duplicate SKU rejection test
   - Duplicate barcode rejection test
   - Product CRUD tests
   - Search/filter tests
   - Validation error tests

### Deliverables
- Complete product management CRUD
- Product API with all endpoints
- Flutter product screens
- Product service layer
- Comprehensive tests

---

## Phase 4: Batch & Inventory Management (COMPLETED)

**Duration**: 1.5 sprints
**Depends On**: Phase 3

### Completed
- [x] Batch list/read model with branch/product filters and derived batch state
- [x] Inventory API with paged branch/product stock, stock status, active batches, nearest expiry, and operational valuation
- [x] Controlled opening stock command
- [x] Controlled stock increase/decrease adjustment command
- [x] Controlled damaged-stock removal through the adjustment flow
- [x] Physical stock count reconciliation using variance movements
- [x] Expiry view and expired stock disposal command
- [x] Read-only stock movement history
- [x] FEFO preview that does not mutate stock
- [x] Inventory integrity check comparing ledger, batch projection, and inventory projection
- [x] Inventory permissions seeded idempotently
- [x] PostgreSQL non-negative quantity constraints and query indexes
- [x] Flutter inventory workspace with Stock, Batches, Expiry, and Movements tabs
- [x] Flutter opening stock, adjustment, and stock count dialogs
- [x] Backend unit tests, PostgreSQL integration tests, and Flutter widget tests

### Deliverables
- Batch and inventory management API
- Controlled stock mutation service
- Read-only stock movement ledger API
- FEFO preview for future POS integration
- Flutter inventory screens
- Phase 4 migration: `20260901215409_CompleteBatchAndInventoryManagement`
- Complete batch and inventory tests

### Boundary
- Purchase Orders, purchase receiving, POS, sales, transfers, and reports remain future phases.

---

## Phase 5: Supplier Management (COMPLETED)

**Duration**: 1 sprint
**Depends On**: Phase 2

### Completed
1. **Supplier API**
   - GET /api/suppliers
   - GET /api/suppliers/lookup
   - GET /api/suppliers/{id}
   - POST /api/suppliers
   - PUT /api/suppliers/{id}
   - POST /api/suppliers/{id}/activate
   - POST /api/suppliers/{id}/deactivate
   - GET /api/suppliers/{id}/ledger
   - POST /api/suppliers/{id}/payments
   - POST /api/suppliers/{id}/adjustments

2. **Supplier Financial Foundation**
   - Global supplier master with normalized unique supplier names
   - Branch-scoped immutable supplier ledger
   - Opening balance, payment, debit adjustment, and credit adjustment entries
   - Positive payable / negative advance sign convention
   - Supplier ledger PostgreSQL check constraints and indexes

3. **Flutter UI**
   - Supplier list/search
   - Add and edit supplier dialogs
   - Activation/deactivation actions
   - Ledger statement dialog
   - Payment and adjustment dialogs
   - Permission-aware navigation and actions

### Deliverables
- Supplier management API and application service
- Supplier ledger source-of-truth foundation
- Phase 5 migration: `20260902051500_CompleteSupplierManagement`
- Flutter supplier screens
- Backend, PostgreSQL integration, and Flutter widget tests

### Boundary
- Purchase returns, accounting general ledger, POS, sales, and reports remain future phases.

---

## Phase 6: Purchase Orders & Receiving (COMPLETED)

**Duration**: 2 sprints
**Depends On**: Phase 4, Phase 5

### Completed
1. **Purchase Order Foundation**
   - Branch-scoped purchase orders and purchase order items
   - Draft, submitted, partially received, completed, and cancelled states
   - Ordered vs received quantity tracking

2. **Goods Receiving**
   - Direct purchases and PO-linked receiving
   - Paid and bonus quantity handling
   - Batch creation/reuse with metadata validation
   - Atomic inventory, stock movement, and supplier payable posting

3. **Purchasing API**
   - GET/POST/PUT purchase orders
   - Submit and cancel purchase order actions
   - Goods receipt posting, direct purchase posting, purchase history, and purchasing options

4. **Database and Permissions**
   - Phase 6 migration: `20260902055022_CompletePurchasingAndGoodsReceiving`
   - Purchase/receipt constraints, indexes, and foreign keys verified in PostgreSQL
   - Purchasing permission catalog and role defaults seeded idempotently

5. **Flutter UI**
   - Purchasing navigation is permission-aware
   - Purchase order list/create/submit/cancel
   - Direct purchase and receive-goods dialogs
   - Purchase history display

6. **Tests**
   - Purchasing service behavior tests
   - PostgreSQL integration tests for purchasing constraints and seeds
   - Flutter purchasing widget tests

### Deliverables
- Purchase order and goods receipt domain entities
- Purchasing application service, EF repository, and API controllers
- Phase 6 migration and PostgreSQL verification
- Flutter purchasing screens
- Backend, PostgreSQL integration, and Flutter widget tests

### Boundary
- Purchase returns, POS, sales, accounting general ledger, reports, and supplier payment allocation remain future phases.

---

## Phase 7: POS / Sales Module

**Duration**: 2 sprints
**Depends On**: Phase 4, Phase 6

### Tasks
1. **Sale Entity** (Add to domain)
   - SaleId, BranchId, CustomerId (optional), SaleDate, TotalAmount, Status
   - PaymentMethod, Notes, CreatedByUserId

2. **Sale Item Entity**
   - ProductId, BatchId, Quantity, UnitPrice, DiscountAmount, TotalPrice

3. **Sale API**
   - GET /api/products/stock-available (for POS)
   - POST /api/sales (create sale)
   - GET /api/sales (search, filter by date)
   - GET /api/sales/{id}
   - POST /api/sales/{id}/payment

4. **FEFO Integration**
   - Auto-select batch using FEFO logic
   - Fallback if no valid batch
   - Show selected batch to cashier
   - Allow manual batch override

5. **Sale Validation**
   - Product available in stock
   - Quantity available
   - Valid batch (not expired)
   - Price within discount limits

6. **Flutter POS UI**
   - Product search/barcode scanner
   - Add to cart interface
   - Cart with item list
   - Quantity editor
   - Discount entry
   - Payment methods
   - Print receipt (future)

7. **Sale Recording**
   - Create Sale record
   - Create Sale Items
   - Create StockMovement (Sale type, negative quantity)
   - Update Inventory
   - Close sale transaction

8. **Tests**
   - Sale creation with FEFO
   - Stock deduction
   - Expired batch rejection
   - Discount validation
   - StockMovement generation

### Deliverables
- Complete POS workflow
- Sales management API
- FEFO batch selection
- Flutter POS interface
- Integration tests

---

## Phase 8: Sales Returns

**Duration**: 1 sprint
**Depends On**: Phase 7

### Tasks
1. **Sale Return Entity**
   - ReturnId, SaleId, ReturnDate, Reason, Status
   - Items returned, Refund amount

2. **Sale Return API**
   - POST /api/sales/{id}/return (create return)
   - GET /api/returns
   - PUT /api/returns/{id} (approve/reject)

3. **Return Workflow**
   - Link to original sale
   - Verify items and quantities
   - Create reverse StockMovement (SaleReturn type)
   - Update Inventory
   - Refund processing

4. **Flutter UI**
   - Return creation from sale
   - Return list/search
   - Return detail/approval

5. **Tests**
   - Return creation
   - Inventory restoration
   - StockMovement reversal

### Deliverables
- Sales return management
- Return API
- Flutter return screens
- Tests

---

## Phase 9: Purchase Returns

**Duration**: 1 sprint
**Depends On**: Phase 6

### Tasks
1. **Purchase Return Entity**
   - ReturnId, PurchaseId, ReturnDate, Reason, Status

2. **Purchase Return API**
   - POST /api/purchases/{id}/return
   - GET /api/purchase-returns
   - PUT /api/purchase-returns/{id} (approve)

3. **Return Workflow**
   - Link to original purchase
   - Reduce batch quantity
   - Create StockMovement (PurchaseReturn type)
   - Update Inventory

4. **Tests**
   - Return reduces stock
   - StockMovement created

### Deliverables
- Purchase return management
- API endpoints
- Integration tests

---

## Phase 10: Stock Adjustments

**Duration**: 1 sprint
**Depends On**: Phase 4

### Tasks
1. **Stock Adjustment**
   - Physical stock count vs system
   - Adjustment reasons (damaged, expired, shrinkage, reconciliation)

2. **Adjustment API**
   - POST /api/stock-adjustments (increase/decrease)
   - GET /api/stock-adjustments (history)
   - PUT /api/stock-adjustments/{id} (approve/reject)

3. **Adjustment Workflow**
   - Create adjustment record
   - Create StockMovement (Adjustment type)
   - Update Inventory
   - Audit trail

4. **Tests**
   - Adjustment creates correct StockMovement
   - Inventory updated

### Deliverables
- Stock adjustment functionality
- API endpoints
- Tests

---

## Phase 11: Expiry Management & Disposal

**Duration**: 1 sprint
**Depends On**: Phase 4

### Tasks
1. **Expiry Tracking**
   - GET /api/inventory/expiring-soon (< 30 days, < 7 days)
   - Batch-level expiry status

2. **Expiry Alert System**
   - Daily job to check expiring batches
   - Alert notifications (future: push, email)

3. **Disposal/Write-off**
   - POST /api/batches/{id}/dispose (mark as expired)
   - Create StockMovement (Expired type, negative quantity)
   - Update Inventory to zero
   - Audit trail

4. **Flutter UI**
   - Expiring soon alerts
   - Batch disposal interface

5. **Tests**
   - Expiry detection
   - Disposal process
   - Inventory set to zero

### Deliverables
- Expiry tracking
- Alert system
- Disposal workflow
- Tests

---

## Phase 12: Financial Reports & Accounts

**Duration**: 2 sprints
**Depends On**: Phase 7, Phase 8, Phase 9, Phase 10

### Tasks
1. **Reports Module**
   - Sales report (by date, by product, by cashier)
   - Inventory report (valuation, turnover)
   - Purchase report (by supplier, cost analysis)
   - Return analysis

2. **Financial Reports**
   - Daily sales summary
   - Revenue by product/category
   - Stock value (at cost)
   - Profit & loss analysis (future: detailed)

3. **Report API**
   - GET /api/reports/sales-summary
   - GET /api/reports/inventory-valuation
   - GET /api/reports/expense-summary

4. **Flutter Reports UI**
   - Date range selection
   - Filter by product/category/supplier
   - Export to PDF (future)
   - Chart visualizations (future)

5. **Tests**
   - Report accuracy
   - Calculations correct

### Deliverables
- Report API endpoints
- Financial calculations
- Flutter report screens

---

## Phase 13: Audit Log & Compliance

**Duration**: 1 sprint
**Depends On**: Phase 2

### Tasks
1. **Audit Log Service**
   - Log all sensitive operations
   - Automatic capture (OldValues, NewValues as JSON)
   - IPAddress, UserAgent tracking

2. **Audit Log API**
   - GET /api/audit-logs (search by user, entity, date)
   - GET /api/audit-logs/sensitive (high-priority changes)

3. **Flutter Audit Viewer**
   - Audit log search/filter
   - Change comparison (before/after)
   - User activity timeline

4. **Compliance**
   - Pakistan FBR (Federal Board of Revenue) ready format
   - Immutable audit trail

5. **Tests**
   - Audit logging on all sensitive operations

### Deliverables
- Audit log management
- Compliance reports
- Flutter audit viewer

---

## Phase 14: Data Backup & Recovery

**Duration**: 1 sprint
**Depends On**: All previous

### Tasks
1. **Backup Strategy**
   - Daily automated database backup
   - Backup storage location (local, cloud)
   - Backup retention policy (30 days)

2. **Restore Process**
   - Restore from backup point-in-time
   - Verification after restore

3. **API**
   - GET /api/admin/backups
   - POST /api/admin/restore (admin only)

4. **Tests**
   - Backup creation
   - Restore validation

### Deliverables
- Backup automation
- Restore procedures
- Admin API endpoints

---

## Phase 15: Multi-Branch Operations

**Duration**: 2 sprints
**Depends On**: All previous

### Tasks
1. **Branch Management**
   - Complete branch CRUD
   - Branch hierarchy (head office, sub-branches)
   - Branch users assignment

2. **Inter-Branch Stock Transfer**
   - POST /api/transfers (create transfer request)
   - GET /api/transfers (status tracking)
   - POST /api/transfers/{id}/receive (receive at destination)
   - Create StockMovement (TransferOut, TransferIn)

3. **Multi-Branch Reporting**
   - Consolidated reports across all branches
   - Branch-wise comparison
   - Central inventory view

4. **Flutter Multi-Branch**
   - Branch selector in app
   - Branch-level operations
   - Consolidated dashboard (if admin)

5. **Tests**
   - Transfer workflow
   - Inventory changes at both ends

### Deliverables
- Branch management API
- Inter-branch transfers
- Multi-branch reports
- Flutter multi-branch UI

---

## Phase 16: Offline Synchronization

**Duration**: 2 sprints
**Depends On**: Phase 15

### Tasks
1. **Local Database**
   - SQLite database setup
   - Initial data download (products, batches, settings)

2. **Offline POS**
   - Create sales offline
   - Queue StockMovements
   - Local Inventory tracking

3. **Sync Engine**
   - Detect connectivity
   - Upload queued movements
   - Download updates
   - Conflict resolution (last-write-wins)

4. **Flutter Implementation**
   - GetIt service locator for DB switching
   - Offline indicator UI
   - Sync status screen

5. **Tests**
   - Offline sale creation
   - Sync on reconnection
   - Conflict handling

### Deliverables
- Offline SQLite database
- Sync engine
- Flutter offline mode
- Integration tests

---

## Phase 17: Advanced Analytics & BI

**Duration**: 2 sprints
**Depends On**: Phase 12

### Tasks
1. **Analytics Dashboard**
   - KPI cards (daily sales, inventory value, profit)
   - Charts (sales trend, inventory turnover, top products)
   - Forecasting (predict stock needs, sales trend)

2. **Predictive Analytics**
   - Sales forecasting
   - Reorder point optimization
   - Expiry risk analysis

3. **Data Export**
   - Export reports to Excel
   - BI tool integration (Power BI, Tableau)

4. **Flutter BI UI**
   - Dashboard with KPIs
   - Interactive charts
   - Drill-down capability

### Deliverables
- Advanced reports
- BI dashboard
- Export functionality

---

## Phase 18: Production Hardening & Launch

**Duration**: 1.5 sprints
**Depends On**: All previous

### Tasks
1. **Performance Optimization**
   - Query optimization
   - Caching layer (Redis)
   - Load testing

2. **Security Hardening**
   - Penetration testing
   - SQL injection prevention verification
   - XSS prevention verification
   - HTTPS/TLS enforcement

3. **Scalability**
   - Load balancer setup
   - Database replication
   - API horizontal scaling

4. **Deployment**
   - Docker containerization
   - Kubernetes ready
   - CI/CD pipeline setup
   - Automated testing

5. **Documentation**
   - Deployment guide
   - Operation manual
   - Troubleshooting guide
   - API documentation (Swagger)

6. **User Training**
   - User documentation
   - Video tutorials
   - Support process

7. **Go-Live**
   - Data migration from legacy
   - Parallel running (if needed)
   - Cutover process
   - Support readiness

### Deliverables
- Production-ready application
- Deployment automation
- Complete documentation
- Training materials

---

## Timeline Estimate

| Phase | Duration | Cumulative |
|-------|----------|-----------|
| 1: Foundation | 1 sprint | 1 sprint |
| 2: Authentication | 1 sprint | 2 sprints |
| 3: Product Master | 1.5 sprints | 3.5 sprints |
| 4: Batch & Inventory | 1.5 sprints | 5 sprints |
| 5: Suppliers | 1 sprint | 6 sprints |
| 6: Purchases | 2 sprints | 8 sprints |
| 7: POS | 2 sprints | 10 sprints |
| 8: Sales Returns | 1 sprint | 11 sprints |
| 9: Purchase Returns | 1 sprint | 12 sprints |
| 10: Stock Adjustments | 1 sprint | 13 sprints |
| 11: Expiry Management | 1 sprint | 14 sprints |
| 12: Financial Reports | 2 sprints | 16 sprints |
| 13: Audit Logs | 1 sprint | 17 sprints |
| 14: Backup & Recovery | 1 sprint | 18 sprints |
| 15: Multi-Branch | 2 sprints | 20 sprints |
| 16: Offline Sync | 2 sprints | 22 sprints |
| 17: Advanced Analytics | 2 sprints | 24 sprints |
| 18: Production Launch | 1.5 sprints | 25.5 sprints |

**Total**: ~25.5 sprints (~6 months with 1-week sprints, or ~12 months with 2-week sprints)

---

## Success Criteria by Phase

### Phase 1 ✅
- [x] All 13 domain entities created with relationships
- [x] Database migrations working
- [x] Flutter app compiles and runs
- [x] Architecture documented

### Phase 2
- [x] Full auth flow tested end-to-end
- [x] 7 roles seeded with permissions
- [x] Token generation and validation working
- [x] Login screen connects to API

### Phase 3
- [x] Product/category/manufacturer operations working
- [x] Normalized SKU/barcode/name constraints verified in PostgreSQL
- [x] Flutter Product Master screens functional
- [x] Backend, PostgreSQL integration, and Flutter widget tests passing

### Phase 4
- [x] Opening stock, adjustment, stock count, expiry disposal, and FEFO preview use controlled backend workflows
- [x] Stock movements remain immutable transaction history
- [x] Batch and inventory projections are protected by application logic and PostgreSQL constraints
- [x] Flutter inventory screens are permission-aware
- [x] Backend, PostgreSQL integration, and Flutter widget tests passing

### Phase 5
- [x] Supplier master create/edit/search/lookup workflows are permission-aware
- [x] Supplier activation/deactivation is explicit; suppliers are not deleted
- [x] Supplier ledger is immutable and branch-scoped
- [x] Opening balances, payments, and debit/credit adjustments follow the documented sign convention
- [x] PostgreSQL constraints and indexes verified for suppliers and supplier ledger entries
- [x] Backend, PostgreSQL integration, and Flutter widget tests passing

### Phase 6
- [x] Purchase orders support create, submit, cancel, and received quantity tracking
- [x] Goods receipt posting updates batches, inventory projections, stock movements, and supplier ledger entries atomically
- [x] Direct purchases and PO-linked receiving are permission-aware
- [x] Paid vs bonus quantity and supplier invoice uniqueness rules are enforced
- [x] PostgreSQL constraints and indexes verified for purchase orders and goods receipts
- [x] Backend, PostgreSQL integration, and Flutter widget tests passing

### Phase 7+
Similar criteria for each remaining phase...

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Database not performing | Implement caching layer early, optimize queries |
| Scope creep | Strict phase gates, feature review board |
| Team capacity | Parallel phase work after foundation solid |
| Third-party integration | Abstract interfaces, mock implementations |
| Data loss | Backup system in Phase 14, not critical path |

---

## Success Metrics

- Time to deploy: < 5 minutes
- Test coverage: > 80% on business logic
- API response time: < 200ms (p95)
- System uptime: > 99.5%
- User adoption: > 90% within 3 months
