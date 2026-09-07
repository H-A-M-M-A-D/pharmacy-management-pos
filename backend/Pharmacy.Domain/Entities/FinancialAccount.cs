using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class FinancialAccount : Entity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public FinancialAccountType AccountType { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public ICollection<FinancialLedgerEntry> LedgerEntries { get; set; } = [];
}

public enum FinancialAccountType
{
    Cash = 1,
    Bank = 2,
    MobileWallet = 3,
    CardSettlement = 4,
    Other = 5
}
