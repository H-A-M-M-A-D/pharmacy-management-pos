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
    public const string SalesHold = "sales.hold";
    public const string SalesDiscount = "sales.discount";
    public const string SalesReprint = "sales.reprint";

    public static readonly IReadOnlyList<string> All =
    [
        UsersView, UsersCreate, UsersUpdate, UsersActivate, UsersDeactivate,
        UsersResetPassword, UsersManageOwner, RolesView, RolesManage,
        PermissionsView, ProfileView, ProfileUpdate, ProfileChangePassword, AuditView,
        ProductsView, ProductsCreate, ProductsUpdate, ProductsActivate, ProductsDeactivate,
        CategoriesView, CategoriesManage, ManufacturersView, ManufacturersManage,
        InventoryView, InventoryOpeningStock, InventoryAdjust, InventoryStockCount,
        InventoryExpiryManage, InventoryMovementsView, BatchesView,
        SuppliersView, SuppliersCreate, SuppliersUpdate, SuppliersActivate,
        SuppliersDeactivate, SuppliersLedgerView, SuppliersPaymentCreate,
        SuppliersAdjustBalance,
        PurchasesView, PurchasesCreate, PurchasesUpdateDraft, PurchasesCancel,
        PurchasesReceive, PurchaseOrdersView, PurchaseOrdersCreate,
        PurchaseOrdersUpdate, PurchaseOrdersCancel,
        SalesView, SalesCreate, SalesHold, SalesDiscount, SalesReprint
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
