namespace Pennywise.Models;

public class Category
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "expense"; // "expense" or "income"
    public string Icon { get; set; } = "tag";
    public string Color { get; set; } = "#165E34";
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int TransactionCount { get; set; } = 0;
}
