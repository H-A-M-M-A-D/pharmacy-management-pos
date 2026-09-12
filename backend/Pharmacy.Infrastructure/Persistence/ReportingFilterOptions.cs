using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    public async Task<object> FilterOptionsAsync(User actor, CancellationToken ct)
    {
        var all = actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager;
        var branches = await db.Branches.AsNoTracking().Where(x => all || x.Id == actor.BranchId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var godowns = await db.Godowns.AsNoTracking().Where(x => all || x.BranchId == actor.BranchId && x.UserGodowns.Any(g => g.UserId == actor.Id))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, x.Name, x.BranchId }).ToListAsync(ct);
        var products = await db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var categories = await db.ProductCategories.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var manufacturers = await db.Manufacturers.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var customers = await db.Customers.AsNoTracking().Where(x => all || x.Sales.Any(s => s.BranchId == actor.BranchId) || x.LedgerEntries.Any(l => l.BranchId == actor.BranchId))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var suppliers = await db.Suppliers.AsNoTracking().Where(x => all || db.GoodsReceipts.Any(r => r.SupplierId == x.Id && r.BranchId == actor.BranchId) || db.SupplierLedgerEntries.Any(l => l.SupplierId == x.Id && l.BranchId == actor.BranchId))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var users = await db.Users.AsNoTracking().Where(x => all || x.BranchId == actor.BranchId).OrderBy(x => x.FullName).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, Name = x.FullName }).ToListAsync(ct);
        var priceLevels = await db.PriceLevels.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Take(200).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        return new { Branches = branches, Godowns = godowns, Products = products, Categories = categories, Manufacturers = manufacturers, Customers = customers, Suppliers = suppliers, Users = users, PriceLevels = priceLevels,
            LookupLimit = 200 };
    }
}
