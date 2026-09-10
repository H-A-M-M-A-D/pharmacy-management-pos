using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

/// <summary>
/// Shared "what open documents does this party still owe against, as of now" query, used by
/// <see cref="CustomerRepository"/>/<see cref="SupplierRepository"/> for default FIFO payment
/// allocation and by <see cref="VoucherRepository"/> for the same purpose when a voucher targets
/// a customer/supplier. Kept in exactly one place so the "outstanding = original - allocated -
/// return credit" computation can never drift between callers.
/// </summary>
internal static class OpenDocumentQueries
{
    public static async Task<IReadOnlyList<OpenReceivableDto>> GetOpenReceivablesAsync(PharmacyDbContext context, Guid customerId, Guid? branchId, CancellationToken cancellationToken)
    {
        var sales = await context.Sales.AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.Status == SaleStatus.Posted && s.CreditAmount > 0)
            .Where(s => !branchId.HasValue || s.BranchId == branchId)
            .Select(s => new { s.Id, s.BranchId, s.InvoiceNumber, s.PostedAtUtc, s.DueDateUtc, s.CreditAmount })
            .ToListAsync(cancellationToken);
        if (sales.Count == 0) return [];
        var saleIds = sales.Select(s => s.Id).ToList();
        var allocatedMap = (await context.CustomerPaymentAllocations.AsNoTracking()
            .Where(a => saleIds.Contains(a.SaleId))
            .GroupBy(a => a.SaleId)
            .Select(g => new { SaleId = g.Key, Total = g.Sum(x => x.AllocatedAmount) })
            .ToListAsync(cancellationToken)).ToDictionary(x => x.SaleId, x => x.Total);
        var returnMap = (await context.SalesReturns.AsNoTracking()
            .Where(r => saleIds.Contains(r.OriginalSaleId))
            .GroupBy(r => r.OriginalSaleId)
            .Select(g => new { SaleId = g.Key, Total = g.Sum(x => x.CustomerCreditReductionAmount) })
            .ToListAsync(cancellationToken)).ToDictionary(x => x.SaleId, x => x.Total);
        var result = new List<OpenReceivableDto>();
        foreach (var s in sales)
        {
            var outstanding = decimal.Round(s.CreditAmount - allocatedMap.GetValueOrDefault(s.Id) - returnMap.GetValueOrDefault(s.Id), 2);
            if (outstanding <= 0) continue;
            result.Add(new OpenReceivableDto(s.Id, customerId, s.BranchId, s.InvoiceNumber, DateOnly.FromDateTime(s.PostedAtUtc!.Value), s.DueDateUtc, s.CreditAmount, outstanding));
        }
        return result.OrderBy(x => x.DueDateUtc ?? DateTime.MaxValue).ThenBy(x => x.DocumentDate).ToList();
    }

    public static async Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(PharmacyDbContext context, Guid supplierId, Guid? branchId, CancellationToken cancellationToken)
    {
        var receipts = await context.GoodsReceipts.AsNoTracking()
            .Where(r => r.SupplierId == supplierId && r.Status == GoodsReceiptStatus.Posted && r.NetTotal > 0)
            .Where(r => !branchId.HasValue || r.BranchId == branchId)
            .Select(r => new { r.Id, r.BranchId, r.GrnNumber, r.ReceiptDate, r.DueDate, r.NetTotal })
            .ToListAsync(cancellationToken);
        if (receipts.Count == 0) return [];
        var receiptIds = receipts.Select(r => r.Id).ToList();
        var allocatedMap = (await context.SupplierPaymentAllocations.AsNoTracking()
            .Where(a => receiptIds.Contains(a.GoodsReceiptId))
            .GroupBy(a => a.GoodsReceiptId)
            .Select(g => new { GoodsReceiptId = g.Key, Total = g.Sum(x => x.AllocatedAmount) })
            .ToListAsync(cancellationToken)).ToDictionary(x => x.GoodsReceiptId, x => x.Total);
        var returnMap = (await context.PurchaseReturns.AsNoTracking()
            .Where(r => receiptIds.Contains(r.OriginalGoodsReceiptId))
            .GroupBy(r => r.OriginalGoodsReceiptId)
            .Select(g => new { GoodsReceiptId = g.Key, Total = g.Sum(x => x.NetSupplierCredit) })
            .ToListAsync(cancellationToken)).ToDictionary(x => x.GoodsReceiptId, x => x.Total);
        var result = new List<OpenPayableDto>();
        foreach (var r in receipts)
        {
            var outstanding = decimal.Round(r.NetTotal - allocatedMap.GetValueOrDefault(r.Id) - returnMap.GetValueOrDefault(r.Id), 2);
            if (outstanding <= 0) continue;
            result.Add(new OpenPayableDto(r.Id, supplierId, r.BranchId, r.GrnNumber, r.ReceiptDate, r.DueDate, r.NetTotal, outstanding));
        }
        return result.OrderBy(x => x.DueDate ?? DateOnly.MaxValue).ThenBy(x => x.DocumentDate).ToList();
    }
}
