using Pennywise.Data;
using Pennywise.Models;

namespace Pennywise.Repositories;

public class CategoryRepository
{
    private readonly DatabaseContext _db;

    public CategoryRepository(DatabaseContext db)
    {
        _db = db;
    }

    public List<Category> GetCategories(string userId, string? type = null)
    {
        var list = new List<Category>();
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();

        string query = @"
            SELECT c.Id, c.UserId, c.Name, c.Type, c.Icon, c.Color, c.IsDefault, c.CreatedAt,
                   (SELECT COUNT(*) FROM Transactions t WHERE t.CategoryId = c.Id AND t.UserId = c.UserId) AS TxCount
            FROM Categories c
            WHERE c.UserId = @userId
        ";

        if (!string.IsNullOrEmpty(type))
        {
            query += " AND c.Type = @type";
            cmd.AddWithValue("@type", type.ToLowerInvariant());
        }

        query += " ORDER BY c.IsDefault DESC, c.Name ASC;";
        cmd.CommandText = query;
        cmd.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Category
            {
                Id = reader.GetString(0),
                UserId = reader.GetString(1),
                Name = reader.GetString(2),
                Type = reader.GetString(3),
                Icon = reader.GetString(4),
                Color = reader.GetString(5),
                IsDefault = reader.GetInt32(6) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                TransactionCount = Convert.ToInt32(reader.GetValue(8))
            });
        }

        return list;
    }

    public Category? GetCategoryById(string userId, string id)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.Id, c.UserId, c.Name, c.Type, c.Icon, c.Color, c.IsDefault, c.CreatedAt,
                   (SELECT COUNT(*) FROM Transactions t WHERE t.CategoryId = c.Id AND t.UserId = c.UserId) AS TxCount
            FROM Categories c
            WHERE c.Id = @id AND c.UserId = @userId;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Category
            {
                Id = reader.GetString(0),
                UserId = reader.GetString(1),
                Name = reader.GetString(2),
                Type = reader.GetString(3),
                Icon = reader.GetString(4),
                Color = reader.GetString(5),
                IsDefault = reader.GetInt32(6) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                TransactionCount = Convert.ToInt32(reader.GetValue(8))
            };
        }
        return null;
    }

    public bool HasBudgets(string userId, string categoryId)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM Budgets WHERE CategoryId = @categoryId AND UserId = @userId);";
        cmd.AddWithValue("@categoryId", categoryId);
        cmd.AddWithValue("@userId", userId);
        return Convert.ToInt64(cmd.ExecuteScalar()) != 0;
    }

    public Category CreateCategory(string userId, CategoryDto dto)
    {
        var category = new Category
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = dto.Name.Trim(),
            Type = dto.Type.Trim().ToLowerInvariant(),
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "tag" : dto.Icon.Trim(),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? "#165E34" : dto.Color.Trim(),
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
            TransactionCount = 0
        };

        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Categories (Id, UserId, Name, Type, Icon, Color, IsDefault, CreatedAt)
            VALUES (@id, @userId, @name, @type, @icon, @color, 0, @createdAt);
        ";
        cmd.AddWithValue("@id", category.Id);
        cmd.AddWithValue("@userId", category.UserId);
        cmd.AddWithValue("@name", category.Name);
        cmd.AddWithValue("@type", category.Type);
        cmd.AddWithValue("@icon", category.Icon);
        cmd.AddWithValue("@color", category.Color);
        cmd.AddWithValue("@createdAt", category.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        return category;
    }

    public bool UpdateCategory(string userId, string id, CategoryDto dto)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Categories
            SET Name = @name, Type = @type, Icon = @icon, Color = @color
            WHERE Id = @id AND UserId = @userId;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);
        cmd.AddWithValue("@name", dto.Name.Trim());
        cmd.AddWithValue("@type", dto.Type.Trim().ToLowerInvariant());
        cmd.AddWithValue("@icon", string.IsNullOrWhiteSpace(dto.Icon) ? "tag" : dto.Icon.Trim());
        cmd.AddWithValue("@color", string.IsNullOrWhiteSpace(dto.Color) ? "#165E34" : dto.Color.Trim());

        return cmd.ExecuteNonQuery() > 0;
    }

    public (bool Success, string Message) DeleteCategory(string userId, string id)
    {
        using var conn = _db.CreateConnection();

        // Check if category has transactions
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM Transactions WHERE CategoryId = @id AND UserId = @userId;";
            countCmd.AddWithValue("@id", id);
            countCmd.AddWithValue("@userId", userId);
            long txCount = (long)(countCmd.ExecuteScalar() ?? 0L);
            if (txCount > 0)
            {
                return (false, "Cannot delete category that has existing transactions. Please reassign or delete the transactions first.");
            }
        }

        // Check if category has budgets
        using (var budgetCmd = conn.CreateCommand())
        {
            budgetCmd.CommandText = "SELECT COUNT(*) FROM Budgets WHERE CategoryId = @id AND UserId = @userId;";
            budgetCmd.AddWithValue("@id", id);
            budgetCmd.AddWithValue("@userId", userId);
            long bCount = (long)(budgetCmd.ExecuteScalar() ?? 0L);
            if (bCount > 0)
            {
                return (false, "Cannot delete category that has an active budget. Please delete the budget first.");
            }
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Categories WHERE Id = @id AND UserId = @userId;";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@userId", userId);
        int rows = cmd.ExecuteNonQuery();

        return rows > 0 ? (true, "Category deleted successfully.") : (false, "Category not found.");
    }
}
