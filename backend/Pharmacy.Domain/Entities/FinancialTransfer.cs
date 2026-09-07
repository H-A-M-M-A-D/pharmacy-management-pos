using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class FinancialTransfer : Entity
{
    public required string TransferNumber { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid SourceAccountId { get; set; }
    public FinancialAccount? SourceAccount { get; set; }
    public Guid DestinationAccountId { get; set; }
    public FinancialAccount? DestinationAccount { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
