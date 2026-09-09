namespace Pharmacy.Application.Security;

public static class PermissionCatalog
{
    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";
    public const string UsersUpdate = "users.update";
    public const string UsersActivate = "users.activate";
    public const string UsersDeactivate = "users.deactivate";
    public const string UsersResetPassword = "users.reset_password";
    public const string UsersManageOwner = "users.manage_owner";
    public const string RolesView = "roles.view";
    public const string RolesManage = "roles.manage";
    public const string PermissionsView = "permissions.view";
    public const string ProfileView = "profile.view";
    public const string ProfileUpdate = "profile.update";
    public const string ProfileChangePassword = "profile.change_password";
    public const string AuditView = "audit.view";
    public const string ProductsView = "products.view";
    public const string ProductsCreate = "products.create";
    public const string ProductsUpdate = "products.update";
    public const string ProductsActivate = "products.activate";
    public const string ProductsDeactivate = "products.deactivate";
    public const string CategoriesView = "categories.view";
    public const string CategoriesManage = "categories.manage";
    public const string ManufacturersView = "manufacturers.view";
    public const string ManufacturersManage = "manufacturers.manage";
    public const string InventoryView = "inventory.view";
    public const string InventoryOpeningStock = "inventory.opening_stock";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryStockCount = "inventory.stock_count";
    public const string InventoryStockCountView = "inventory.stock_count.view";
    public const string InventoryStockCountFinalize = "inventory.stock_count.finalize";
    public const string InventoryExpiryManage = "inventory.expiry_manage";
    public const string InventoryMovementsView = "inventory.movements.view";
    public const string BatchesView = "batches.view";
    public const string SuppliersView = "suppliers.view";
    public const string SuppliersCreate = "suppliers.create";
    public const string SuppliersUpdate = "suppliers.update";
    public const string SuppliersActivate = "suppliers.activate";
    public const string SuppliersDeactivate = "suppliers.deactivate";
    public const string SuppliersLedgerView = "suppliers.ledger.view";
    public const string SuppliersPaymentCreate = "suppliers.payment.create";
    public const string SuppliersAdjustBalance = "suppliers.adjust_balance";
    public const string CustomersView = "customers.view";
    public const string CustomersCreate = "customers.create";
    public const string CustomersUpdate = "customers.update";
    public const string CustomersActivate = "customers.activate";
    public const string CustomersDeactivate = "customers.deactivate";
    public const string CustomersLedgerView = "customers.ledger.view";
    public const string CustomersPaymentCreate = "customers.payment.create";
    public const string CustomersAdjustBalance = "customers.adjust_balance";
    public const string PurchasesView = "purchases.view";
    public const string PurchasesCreate = "purchases.create";
    public const string PurchasesUpdateDraft = "purchases.update_draft";
    public const string PurchasesCancel = "purchases.cancel";
    public const string PurchasesReceive = "purchases.receive";
    public const string PurchaseOrdersView = "purchase_orders.view";
    public const string PurchaseOrdersCreate = "purchase_orders.create";
    public const string PurchaseOrdersUpdate = "purchase_orders.update";
    public const string PurchaseOrdersCancel = "purchase_orders.cancel";
    public const string SalesView = "sales.view";
    public const string SalesCreate = "sales.create";
    public const string SalesCredit = "sales.credit";
    public const string SalesHold = "sales.hold";
    public const string SalesDiscount = "sales.discount";
    public const string SalesReprint = "sales.reprint";
    public const string SalesReturnsView = "sales.returns.view";
    public const string SalesReturnsCreate = "sales.returns.create";
    public const string SalesReturnsRefund = "sales.returns.refund";
    public const string SalesReturnsReprint = "sales.returns.reprint";
    public const string PurchaseReturnsView = "purchase_returns.view";
    public const string PurchaseReturnsCreate = "purchase_returns.create";
    public const string PurchaseReturnsReprint = "purchase_returns.reprint";
    public const string CashierShiftOpen = "cashier_shift.open";
    public const string CashierShiftView = "cashier_shift.view";
    public const string CashierShiftClose = "cashier_shift.close";
    public const string CashierShiftCloseAny = "cashier_shift.close_any";
    public const string CashierShiftReconcile = "cashier_shift.reconcile";
    public const string CashierShiftDrawerAdjust = "cashier_shift.drawer_adjust";
    public const string AccountsView = "accounts.view";
    public const string AccountsManage = "accounts.manage";
    public const string AccountsCoaView = "accounts.coa.view";
    public const string AccountsCoaManage = "accounts.coa.manage";
    public const string AccountsJournalView = "accounts.journal.view";
    public const string AccountsJournalPost = "accounts.journal.post";
    public const string ExpensesView = "expenses.view";
    public const string ExpensesCreate = "expenses.create";
    public const string ExpensesPost = "expenses.post";
    public const string FinanceLedgerView = "finance.ledger.view";
    public const string FinanceIncomeCreate = "finance.income.create";
    public const string FinanceTransfer = "finance.transfer";
    public const string FinanceAdjust = "finance.adjust";
    public const string ReportsView = "reports.view";
    public const string ReportsSales = "reports.sales";
    public const string ReportsPurchases = "reports.purchases";
    public const string ReportsInventory = "reports.inventory";
    public const string ReportsFinancial = "reports.financial";
    public const string ReportsProfitability = "reports.profitability";
    public const string ReportsExport = "reports.export";
    public const string SystemView = "system.view";
    public const string SystemSettingsManage = "system.settings.manage";
    public const string SystemBackup = "system.backup";
    public const string BranchesView = "branches.view";
    public const string BranchesManage = "branches.manage";
    public const string RecycleBinView = "recycle_bin.view";
    public const string RecycleBinRestore = "recycle_bin.restore";
    public const string AuditExport = "audit.export";

    public static readonly IReadOnlyList<string> All =
    [
        UsersView, UsersCreate, UsersUpdate, UsersActivate, UsersDeactivate,
        UsersResetPassword, UsersManageOwner, RolesView, RolesManage,
        PermissionsView, ProfileView, ProfileUpdate, ProfileChangePassword, AuditView,
        ProductsView, ProductsCreate, ProductsUpdate, ProductsActivate, ProductsDeactivate,
        CategoriesView, CategoriesManage, ManufacturersView, ManufacturersManage,
        InventoryView, InventoryOpeningStock, InventoryAdjust, InventoryStockCount,
        InventoryStockCountView, InventoryStockCountFinalize,
        InventoryExpiryManage, InventoryMovementsView, BatchesView,
        SuppliersView, SuppliersCreate, SuppliersUpdate, SuppliersActivate,
        SuppliersDeactivate, SuppliersLedgerView, SuppliersPaymentCreate,
        SuppliersAdjustBalance,
        CustomersView, CustomersCreate, CustomersUpdate, CustomersActivate,
        CustomersDeactivate, CustomersLedgerView, CustomersPaymentCreate,
        CustomersAdjustBalance,
        PurchasesView, PurchasesCreate, PurchasesUpdateDraft, PurchasesCancel,
        PurchasesReceive, PurchaseOrdersView, PurchaseOrdersCreate,
        PurchaseOrdersUpdate, PurchaseOrdersCancel,
        SalesView, SalesCreate, SalesCredit, SalesHold, SalesDiscount, SalesReprint,
        SalesReturnsView, SalesReturnsCreate, SalesReturnsRefund, SalesReturnsReprint,
        PurchaseReturnsView, PurchaseReturnsCreate, PurchaseReturnsReprint,
        CashierShiftOpen, CashierShiftView, CashierShiftClose, CashierShiftCloseAny,
        CashierShiftReconcile, CashierShiftDrawerAdjust,
        AccountsView, AccountsManage, AccountsCoaView, AccountsCoaManage,
        AccountsJournalView, AccountsJournalPost,
        ExpensesView, ExpensesCreate, ExpensesPost,
        FinanceLedgerView, FinanceIncomeCreate, FinanceTransfer, FinanceAdjust,
        ReportsView, ReportsSales, ReportsPurchases, ReportsInventory, ReportsFinancial,
        ReportsProfitability, ReportsExport,
        SystemView, SystemSettingsManage, SystemBackup, BranchesView, BranchesManage,
        RecycleBinView, RecycleBinRestore, AuditExport
    ];
}

public static class RoleCatalog
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Pharmacist = "Pharmacist";
    public const string Cashier = "Cashier";
    public const string PurchaseManager = "PurchaseManager";
    public const string Accountant = "Accountant";
    public const string StoreKeeper = "StoreKeeper";

    public static readonly IReadOnlyList<string> All =
    [Owner, Manager, Pharmacist, Cashier, PurchaseManager, Accountant, StoreKeeper];
}

public sealed class AuthenticationSecurityOptions
{
    public const string SectionName = "AuthenticationSecurity";

    public int MaximumFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
