using System.Data.Common;
using Pennywise.Data;
using Pennywise.Models;

namespace Pennywise.Repositories;

public class TransactionRepository
{
    private readonly DatabaseContext _db;

    public TransactionRepository(DatabaseContext db)
    {
        _db = db;
    }

    public (List<Transaction> Items, int TotalCount, decimal TotalIncome, decimal TotalExpenses) GetTransactions(
        string userId,
        string? type = null,
        string? categoryId = null,
        string? startDate = null,
        string? endDate = null,
        string? search = null,
        int page = 1,
        int pageSize = 10,
        string? sortBy = "date",
        string? sortOrder = "desc")
    {
        using var conn = _db.CreateConnection();

        string whereClause = "WHERE t.UserId = @userId";
        var parameters = new List<(string Name, object Value)>
        {
            ("@userId", userId)
        };

        if (!string.IsNullOrWhiteSpace(type) && type.ToLowerInvariant() != "all")
        {
            whereClause += " AND t.Type = @type";
            parameters.Add(("@type", type.ToLowerInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(categoryId) && categoryId.ToLowerInvariant() != "all")
        {
            whereClause += " AND t.CategoryId = @categoryId";
            parameters.Add(("@categoryId", categoryId));
        }

        if (!string.IsNullOrWhiteSpace(startDate))
        {
            whereClause += " AND t.Date >= @startDate";
            parameters.Add(("@startDate", startDate));
        }

        if (!string.IsNullOrWhiteSpace(endDate))
        {
            whereClause += " AND t.Date <= @endDate";
            parameters.Add(("@endDate", endDate));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereClause += " AND (LOWER(t.Title) LIKE LOWER(@search) OR LOWER(t.Notes) LIKE LOWER(@search))";
            parameters.Add(("@search", $"%{search}%"));
        }

        // Totals for this filter
        decimal totalIncome = 0;
        decimal totalExpenses = 0;
        int totalCount = 0;

        using (var totalsCmd = conn.CreateCommand())
        {
            totalsCmd.CommandText = $@"
                SELECT 
                    COUNT(*),
                    COALESCE(SUM(CASE WHEN t.Type = 'income' THEN t.Amount ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN t.Type = 'expense' THEN t.Amount ELSE 0 END), 0)
                FROM Transactions t
                {whereClause};
            ";
            foreach (var p in parameters)
            {
                totalsCmd.AddWithValue(p.Name, p.Value);
            }

            using var totalsReader = totalsCmd.ExecuteReader();
            if (totalsReader.Read())
            {
                totalCount = Convert.ToInt32(totalsReader.GetValue(0));
                totalIncome = Convert.ToDecimal(totalsReader.GetDouble(1));
                totalExpenses = Convert.ToDecimal(totalsReader.GetDouble(2));
            }
        }

        // Pagination and Sorting
        string orderByCol = sortBy?.ToLowerInvariant() switch
        {
            "amount" => "t.Amount",
            "title" => "t.Title",
            _ => "t.Date"
        };
        string orderDir = sortOrder?.ToLowerInvariant() == "asc" ? "ASC" : "DESC";

        int offset = Math.Max(0, (page - 1) * pageSize);

        var list = new List<Transaction>();
        using (var dataCmd = conn.CreateCommand())
        {
            dataCmd.CommandText = $@"
                SELECT t.Id, t.UserId, t.CategoryId, c.Name AS CategoryName, c.Icon AS CategoryIcon, c.Color AS CategoryColor,
                       t.Title, t.Amount, t.Type, t.Date, t.Notes, t.CreatedAt
                FROM Transactions t
                JOIN Categories c ON t.CategoryId = c.Id
                {whereClause}
                ORDER BY {orderByCol} {orderDir}, t.CreatedAt DESC
                LIMIT @pageSize OFFSET @offset;
            ";
            foreach (var p in parameters)
            {
                dataCmd.AddWithValue(p.Name, p.Value);
            }
            dataCmd.AddWithValue("@pageSize", pageSize);
            dataCmd.AddWithValue("@offset", offset);

            using var reader = dataCmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Transaction
                {
                    Id = reader.GetString(0),
                    UserId = reader.GetString(1),
                    CategoryId = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    CategoryIcon = reader.GetString(4),
                    CategoryColor = reader.GetString(5),
                    Title = reader.GetString(6),
                    Amount = Convert.ToDecimal(reader.GetDouble(7)),
                    Type = reader.GetString(8),
                    Date = reader.GetString(9),
                    Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
                    CreatedAt = DateTime.Parse(reader.GetString(11))
                });
            }
        }

        return (list, totalCount, totalIncome, totalExpenses);
    }

    public Transaction? GetTransactionById(string userId, string id)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT t.Id, t.UserId, t.CategoryId, c.Name, c.Icon, c.Color,
                   t.Title, t.Amount, t.Type, t.Date, t.Notes, t.CreatedAt
            FROM Transactions t
            JOIN Categories c ON t.CategoryId = c.Id
            WHERE t.Id = @id AND t.UserId = @userId;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Transaction
            {
                Id = reader.GetString(0),
                UserId = reader.GetString(1),
                CategoryId = reader.GetString(2),
                CategoryName = reader.GetString(3),
                CategoryIcon = reader.GetString(4),
                CategoryColor = reader.GetString(5),
                Title = reader.GetString(6),
                Amount = Convert.ToDecimal(reader.GetDouble(7)),
                Type = reader.GetString(8),
                Date = reader.GetString(9),
                Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
                CreatedAt = DateTime.Parse(reader.GetString(11))
            };
        }
        return null;
    }

    public Transaction CreateTransaction(string userId, TransactionDto dto)
    {
        var tx = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            CategoryId = dto.CategoryId,
            Title = dto.Title.Trim(),
            Amount = Math.Abs(dto.Amount),
            Type = dto.Type.Trim().ToLowerInvariant(),
            Date = string.IsNullOrWhiteSpace(dto.Date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : dto.Date.Trim(),
            Notes = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Transactions (Id, UserId, CategoryId, Title, Amount, Type, Date, Notes, CreatedAt)
            VALUES (@id, @userId, @categoryId, @title, @amount, @type, @date, @notes, @createdAt);
        ";
        cmd.AddWithValue("@id", tx.Id);
        cmd.AddWithValue("@userId", tx.UserId);
        cmd.AddWithValue("@categoryId", tx.CategoryId);
        cmd.AddWithValue("@title", tx.Title);
        cmd.AddWithValue("@amount", (double)tx.Amount);
        cmd.AddWithValue("@type", tx.Type);
        cmd.AddWithValue("@date", tx.Date);
        cmd.AddWithValue("@notes", (object?)tx.Notes ?? DBNull.Value);
        cmd.AddWithValue("@createdAt", tx.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        return GetTransactionById(userId, tx.Id)!;
    }

    public bool UpdateTransaction(string userId, string id, TransactionDto dto)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Transactions
            SET CategoryId = @categoryId,
                Title = @title,
                Amount = @amount,
                Type = @type,
                Date = @date,
                Notes = @notes
            WHERE Id = @id AND UserId = @userId;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);
        cmd.AddWithValue("@categoryId", dto.CategoryId);
        cmd.AddWithValue("@title", dto.Title.Trim());
        cmd.AddWithValue("@amount", (double)Math.Abs(dto.Amount));
        cmd.AddWithValue("@type", dto.Type.Trim().ToLowerInvariant());
        cmd.AddWithValue("@date", dto.Date.Trim());
        cmd.AddWithValue("@notes", (object?)dto.Notes?.Trim() ?? DBNull.Value);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteTransaction(string userId, string id)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Transactions WHERE Id = @id AND UserId = @userId;";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public (decimal TotalIncome, decimal TotalExpenses) GetOverallTotals(string userId)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                COALESCE(SUM(CASE WHEN Type = 'income' THEN Amount ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN Type = 'expense' THEN Amount ELSE 0 END), 0)
            FROM Transactions
            WHERE UserId = @userId;
        ";
        cmd.AddWithValue("@userId", userId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (Convert.ToDecimal(reader.GetDouble(0)), Convert.ToDecimal(reader.GetDouble(1)));
        }
        return (0, 0);
    }
}
