namespace Pennywise.Models;

public class SavingsGoal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; } = 0;
    public string? TargetDate { get; set; }
    public string? Description { get; set; }
    public decimal RemainingAmount { get; set; } = 0;
    public decimal Percentage { get; set; } = 0;
    public bool IsCompleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SavingsTransaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GoalId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = "deposit"; // "deposit" or "withdrawal"
    public decimal Amount { get; set; }
    public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
