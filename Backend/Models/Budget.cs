namespace Pennywise.Models;

public class Budget
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryIcon { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = string.Empty;
    public decimal LimitAmount { get; set; }
    public string Period { get; set; } = "monthly"; // "monthly", "weekly", "yearly"
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public decimal SpentAmount { get; set; } = 0;
    public decimal RemainingAmount { get; set; } = 0;
    public decimal Percentage { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
