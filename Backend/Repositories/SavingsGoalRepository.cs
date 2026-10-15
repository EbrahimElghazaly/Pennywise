using Pennywise.Data;
using Pennywise.Models;

namespace Pennywise.Repositories;

public class SavingsGoalRepository
{
    private readonly DatabaseContext _db;

    public SavingsGoalRepository(DatabaseContext db)
    {
        _db = db;
    }

    public List<SavingsGoal> GetGoals(string userId)
    {
        var list = new List<SavingsGoal>();
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, UserId, Name, TargetAmount, CurrentAmount, TargetDate, Description, CreatedAt
            FROM SavingsGoals
            WHERE UserId = @userId
            ORDER BY CreatedAt DESC;
        ";
        cmd.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var target = Convert.ToDecimal(reader.GetDouble(3));
            var current = Convert.ToDecimal(reader.GetDouble(4));
            var remaining = Math.Max(0, target - current);
            var percentage = target > 0 ? Math.Round((current / target) * 100m, 1) : 0m;
            var isCompleted = current >= target && target > 0;

            list.Add(new SavingsGoal
            {
                Id = reader.GetString(0),
                UserId = reader.GetString(1),
                Name = reader.GetString(2),
                TargetAmount = target,
                CurrentAmount = current,
                TargetDate = reader.IsDBNull(5) ? null : reader.GetString(5),
                Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                RemainingAmount = remaining,
                Percentage = percentage,
                IsCompleted = isCompleted
            });
        }

        return list;
    }

    public SavingsGoal? GetGoalById(string userId, string id)
    {
        var goals = GetGoals(userId);
        return goals.FirstOrDefault(g => g.Id == id);
    }

    public SavingsGoal CreateGoal(string userId, SavingsGoalDto dto)
    {
        var goal = new SavingsGoal
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = dto.Name.Trim(),
            TargetAmount = Math.Abs(dto.TargetAmount),
            CurrentAmount = Math.Max(0, dto.CurrentAmount),
            TargetDate = dto.TargetDate,
            Description = dto.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO SavingsGoals (Id, UserId, Name, TargetAmount, CurrentAmount, TargetDate, Description, CreatedAt)
            VALUES (@id, @userId, @name, @targetAmount, @currentAmount, @targetDate, @description, @createdAt);
        ";
        cmd.AddWithValue("@id", goal.Id);
        cmd.AddWithValue("@userId", goal.UserId);
        cmd.AddWithValue("@name", goal.Name);
        cmd.AddWithValue("@targetAmount", (double)goal.TargetAmount);
        cmd.AddWithValue("@currentAmount", (double)goal.CurrentAmount);
        cmd.AddWithValue("@targetDate", (object?)goal.TargetDate ?? DBNull.Value);
        cmd.AddWithValue("@description", (object?)goal.Description ?? DBNull.Value);
        cmd.AddWithValue("@createdAt", goal.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        return GetGoalById(userId, goal.Id)!;
    }

    public bool UpdateGoal(string userId, string id, SavingsGoalDto dto)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE SavingsGoals
            SET Name = @name,
                TargetAmount = @targetAmount,
                CurrentAmount = @currentAmount,
                TargetDate = @targetDate,
                Description = @description
            WHERE Id = @id AND UserId = @userId;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);
        cmd.AddWithValue("@name", dto.Name.Trim());
        cmd.AddWithValue("@targetAmount", (double)Math.Abs(dto.TargetAmount));
        cmd.AddWithValue("@currentAmount", (double)Math.Max(0, dto.CurrentAmount));
        cmd.AddWithValue("@targetDate", (object?)dto.TargetDate ?? DBNull.Value);
        cmd.AddWithValue("@description", (object?)dto.Description?.Trim() ?? DBNull.Value);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteGoal(string userId, string id)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SavingsGoals WHERE Id = @id AND UserId = @userId;";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public (bool Success, string Message, SavingsGoal? Goal) AddContribution(string userId, string goalId, decimal amount, string? date, string? notes)
    {
        if (amount <= 0) return (false, "Contribution amount must be greater than zero.", null);

        var goal = GetGoalById(userId, goalId);
        if (goal == null) return (false, "Savings goal not found.", null);

        using var conn = _db.CreateConnection();
        using var trans = conn.BeginTransaction();

        using (var txCmd = conn.CreateCommand())
        {
            txCmd.Transaction = trans;
            txCmd.CommandText = @"
                INSERT INTO SavingsTransactions (Id, GoalId, UserId, Type, Amount, Date, Notes, CreatedAt)
                VALUES (@id, @goalId, @userId, 'deposit', @amount, @date, @notes, @createdAt);
            ";
            txCmd.AddWithValue("@id", Guid.NewGuid().ToString());
            txCmd.AddWithValue("@goalId", goalId);
            txCmd.AddWithValue("@userId", userId);
            txCmd.AddWithValue("@amount", (double)amount);
            txCmd.AddWithValue("@date", string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : date);
            txCmd.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
            txCmd.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
            txCmd.ExecuteNonQuery();
        }

        using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.Transaction = trans;
            updateCmd.CommandText = @"
                UPDATE SavingsGoals
                SET CurrentAmount = CurrentAmount + @amount
                WHERE Id = @id AND UserId = @userId;
            ";
            updateCmd.AddWithValue("@amount", (double)amount);
            updateCmd.AddWithValue("@id", goalId);
            updateCmd.AddWithValue("@userId", userId);
            updateCmd.ExecuteNonQuery();
        }

        trans.Commit();
        return (true, "Contribution added successfully.", GetGoalById(userId, goalId));
    }

    public (bool Success, string Message, SavingsGoal? Goal) AddWithdrawal(string userId, string goalId, decimal amount, string? date, string? notes)
    {
        if (amount <= 0) return (false, "Withdrawal amount must be greater than zero.", null);

        var goal = GetGoalById(userId, goalId);
        if (goal == null) return (false, "Savings goal not found.", null);

        using var conn = _db.CreateConnection();
        using var trans = conn.BeginTransaction();

        using (var updateCmd = conn.CreateCommand())
        {
            updateCmd.Transaction = trans;
            updateCmd.CommandText = @"
                UPDATE SavingsGoals
                SET CurrentAmount = CurrentAmount - @amount
                WHERE Id = @id AND UserId = @userId AND CurrentAmount >= @amount;
            ";
            updateCmd.AddWithValue("@amount", (double)amount);
            updateCmd.AddWithValue("@id", goalId);
            updateCmd.AddWithValue("@userId", userId);
            if (updateCmd.ExecuteNonQuery() == 0)
            {
                trans.Rollback();
                var latestGoal = GetGoalById(userId, goalId);
                if (latestGoal == null)
                    return (false, "Savings goal not found.", null);

                return (false, $"Cannot withdraw {amount:N2}. Only {latestGoal.CurrentAmount:N2} is currently saved.", null);
            }
        }

        using (var txCmd = conn.CreateCommand())
        {
            txCmd.Transaction = trans;
            txCmd.CommandText = @"
                INSERT INTO SavingsTransactions (Id, GoalId, UserId, Type, Amount, Date, Notes, CreatedAt)
                VALUES (@id, @goalId, @userId, 'withdrawal', @amount, @date, @notes, @createdAt);
            ";
            txCmd.AddWithValue("@id", Guid.NewGuid().ToString());
            txCmd.AddWithValue("@goalId", goalId);
            txCmd.AddWithValue("@userId", userId);
            txCmd.AddWithValue("@amount", (double)amount);
            txCmd.AddWithValue("@date", string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : date);
            txCmd.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
            txCmd.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
            txCmd.ExecuteNonQuery();
        }

        trans.Commit();
        return (true, "Withdrawal processed successfully.", GetGoalById(userId, goalId));
    }

    public List<SavingsTransaction> GetGoalTransactions(string userId, string goalId)
    {
        var list = new List<SavingsTransaction>();
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, GoalId, UserId, Type, Amount, Date, Notes, CreatedAt
            FROM SavingsTransactions
            WHERE GoalId = @goalId AND UserId = @userId
            ORDER BY Date DESC, CreatedAt DESC;
        ";
        cmd.AddWithValue("@goalId", goalId);
        cmd.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SavingsTransaction
            {
                Id = reader.GetString(0),
                GoalId = reader.GetString(1),
                UserId = reader.GetString(2),
                Type = reader.GetString(3),
                Amount = Convert.ToDecimal(reader.GetDouble(4)),
                Date = reader.GetString(5),
                Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = DateTime.Parse(reader.GetString(7))
            });
        }
        return list;
    }
}
