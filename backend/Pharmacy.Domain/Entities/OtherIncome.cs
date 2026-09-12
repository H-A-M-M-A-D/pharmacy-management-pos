using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class OtherIncome : Entity
{
    public required string IncomeNumber { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    /// <summary>Set once this income has been reversed. See <see cref="Expense.ReversedAtUtc"/> for the
    /// equivalent on the expense side — the same "only these fields may still change" carve-out applies.</summary>
    public DateTime? ReversedAtUtc { get; set; }
    public Guid? ReversedByUserId { get; set; }
    public User? ReversedByUser { get; set; }
    public string? ReversalReason { get; set; }
}
