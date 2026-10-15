using Pennywise.Data;
using Pennywise.Models;

namespace Pennywise.Repositories;

public class BudgetRepository
{
    private readonly DatabaseContext _db;

    public BudgetRepository(DatabaseContext db)
    {
        _db = db;
    }

    public List<Budget> GetBudgets(string userId)
    {
        var list = new List<Budget>();
        using var conn = _db.CreateConnection();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
            SELECT b.Id, b.UserId, b.CategoryId, c.Name AS CategoryName, c.Icon AS CategoryIcon, c.Color AS CategoryColor,
                   b.LimitAmount, b.Period, b.StartDate, b.EndDate, b.CreatedAt
            FROM Budgets b
            JOIN Categories c ON b.CategoryId = c.Id
            WHERE b.UserId = @userId
            ORDER BY b.CreatedAt DESC;
        ";
            cmd.AddWithValue("@userId", userId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var limit = Convert.ToDecimal(reader.GetDouble(6));

                list.Add(new Budget
                {
                    Id = reader.GetString(0),
                    UserId = reader.GetString(1),
                    CategoryId = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    CategoryIcon = reader.GetString(4),
                    CategoryColor = reader.GetString(5),
                    LimitAmount = limit,
                    Period = reader.GetString(7),
                    StartDate = reader.IsDBNull(8) ? null : reader.GetString(8),
                    EndDate = reader.IsDBNull(9) ? null : reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10))
                });
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var budget in list)
        {
            var (startDate, endDate) = GetDateRange(budget, today);
            using var spentCmd = conn.CreateCommand();
            spentCmd.CommandText = @"
                SELECT COALESCE(SUM(Amount), 0)
                FROM Transactions
                WHERE UserId = @userId
                  AND CategoryId = @categoryId
                  AND Type = 'expense'
                  AND Date >= @startDate
                  AND Date <= @endDate;
            ";
            spentCmd.AddWithValue("@userId", userId);
            spentCmd.AddWithValue("@categoryId", budget.CategoryId);
            spentCmd.AddWithValue("@startDate", startDate);
            spentCmd.AddWithValue("@endDate", endDate);
            budget.SpentAmount = Convert.ToDecimal(spentCmd.ExecuteScalar());
            budget.RemainingAmount = budget.LimitAmount - budget.SpentAmount;
            budget.Percentage = budget.LimitAmount > 0
                ? Math.Round((budget.SpentAmount / budget.LimitAmount) * 100m, 1)
                : 0m;
        }

        return list;
    }

    private static (string StartDate, string EndDate) GetDateRange(Budget budget, DateOnly today)
    {
        DateOnly periodStart;
        DateOnly periodEnd;
        switch (budget.Period.ToLowerInvariant())
        {
            case "weekly":
                var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                periodStart = today.AddDays(-daysSinceMonday);
                periodEnd = periodStart.AddDays(6);
                break;
            case "yearly":
                periodStart = new DateOnly(today.Year, 1, 1);
                periodEnd = new DateOnly(today.Year, 12, 31);
                break;
            default:
                periodStart = new DateOnly(today.Year, today.Month, 1);
                periodEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                break;
        }

        var start = string.IsNullOrWhiteSpace(budget.StartDate)
            ? periodStart
            : DateOnly.ParseExact(budget.StartDate, "yyyy-MM-dd");
        var end = string.IsNullOrWhiteSpace(budget.EndDate)
            ? periodEnd
            : DateOnly.ParseExact(budget.EndDate, "yyyy-MM-dd");
        return (start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
    }

    public Budget? GetBudgetById(string userId, string id)
    {
        var budgets = GetBudgets(userId);
        return budgets.FirstOrDefault(b => b.Id == id);
    }

    public Budget CreateBudget(string userId, BudgetDto dto)
    {
        var budget = new Budget
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            CategoryId = dto.CategoryId,
            LimitAmount = Math.Abs(dto.LimitAmount),
            Period = string.IsNullOrWhiteSpace(dto.Period) ? "monthly" : dto.Period.Trim().ToLowerInvariant(),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CreatedAt = DateTime.UtcNow
        };

        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Budgets (Id, UserId, CategoryId, LimitAmount, Period, StartDate, EndDate, CreatedAt)
            VALUES (@id, @userId, @categoryId, @limitAmount, @period, @startDate, @endDate, @createdAt);
        ";
        cmd.AddWithValue("@id", budget.Id);
        cmd.AddWithValue("@userId", budget.UserId);
        cmd.AddWithValue("@categoryId", budget.CategoryId);
        cmd.AddWithValue("@limitAmount", (double)budget.LimitAmount);
        cmd.AddWithValue("@period", budget.Period);
        cmd.AddWithValue("@startDate", (object?)budget.StartDate ?? DBNull.Value);
        cmd.AddWithValue("@endDate", (object?)budget.EndDate ?? DBNull.Value);
        cmd.AddWithValue("@createdAt", budget.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        return GetBudgetById(userId, budget.Id)!;
    }

    public bool UpdateBudget(string userId, string id, BudgetDto dto)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Budgets
            SET CategoryId = @categoryId,
                LimitAmount = @limitAmount,
                Period = @period,
                StartDate = @startDate,
                EndDate = @endDate
            WHERE Id = @id AND UserId = @userId;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);
        cmd.AddWithValue("@categoryId", dto.CategoryId);
        cmd.AddWithValue("@limitAmount", (double)Math.Abs(dto.LimitAmount));
        cmd.AddWithValue("@period", string.IsNullOrWhiteSpace(dto.Period) ? "monthly" : dto.Period.Trim().ToLowerInvariant());
        cmd.AddWithValue("@startDate", (object?)dto.StartDate ?? DBNull.Value);
        cmd.AddWithValue("@endDate", (object?)dto.EndDate ?? DBNull.Value);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteBudget(string userId, string id)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Budgets WHERE Id = @id AND UserId = @userId;";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public (decimal TotalBudget, decimal TotalSpent, decimal Remaining, decimal Percentage) GetMonthlySummary(string userId)
    {
        var budgets = GetBudgets(userId);
        decimal totalBudget = budgets.Sum(b => b.LimitAmount);
        decimal totalSpent = budgets.Sum(b => b.SpentAmount);
        decimal remaining = totalBudget - totalSpent;
        decimal percentage = totalBudget > 0 ? Math.Round((totalSpent / totalBudget) * 100m, 1) : 0m;

        return (totalBudget, totalSpent, remaining, percentage);
    }
}
