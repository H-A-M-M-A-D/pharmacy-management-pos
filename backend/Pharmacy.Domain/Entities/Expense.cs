using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class Expense : Entity
{
    public required string ExpenseNumber { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public DateTime ExpenseDateUtc { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public string? Payee { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime PostedAtUtc { get; set; }
}
