using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;
using Pennywise.Models;

namespace Pennywise.Data;

public class DatabaseContext
{
    private readonly string? _sqliteConnectionString;
    private readonly string? _postgresConnectionString;

    public bool UsesPostgres => _postgresConnectionString is not null;

    public DatabaseContext(string dbPath, string? postgresConnectionString = null)
    {
        if (!string.IsNullOrWhiteSpace(postgresConnectionString))
        {
            _postgresConnectionString = postgresConnectionString;
            return;
        }

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            ForeignKeys = true
        };
        _sqliteConnectionString = connectionString.ToString();
    }

    public DbConnection CreateConnection()
    {
        DbConnection connection = _postgresConnectionString is not null
            ? new NpgsqlConnection(_postgresConnectionString)
            : new SqliteConnection(_sqliteConnectionString);

        connection.Open();
        if (connection is SqliteConnection)
        {
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
            pragma.ExecuteNonQuery();
        }
        return connection;
    }

    public void Initialize()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = UsesPostgres ? PostgresSchema : SqliteSchema;
        cmd.ExecuteNonQuery();
    }

    public void SeedDefaultCategoriesForUser(string userId)
    {
        using var conn = CreateConnection();
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Categories WHERE UserId = @userId;";
        checkCmd.AddWithValue("@userId", userId);
        long count = Convert.ToInt64(checkCmd.ExecuteScalar() ?? 0L);
        if (count > 0) return;

        var defaultCategories = new[]
        {
            (Name: "Food", Type: "expense", Icon: "food", Color: "#FF9800"),
            (Name: "Transportation", Type: "expense", Icon: "car", Color: "#2196F3"),
            (Name: "Shopping", Type: "expense", Icon: "shopping", Color: "#E91E63"),
            (Name: "Bills", Type: "expense", Icon: "receipt", Color: "#3F51B5"),
            (Name: "Healthcare", Type: "expense", Icon: "health", Color: "#009688"),
            (Name: "Education", Type: "expense", Icon: "education", Color: "#673AB7"),
            (Name: "Entertainment", Type: "expense", Icon: "entertainment", Color: "#9C27B0"),
            (Name: "Salary", Type: "income", Icon: "salary", Color: "#4CAF50"),
            (Name: "Freelancing", Type: "income", Icon: "freelance", Color: "#00BCD4"),
            (Name: "Investments", Type: "income", Icon: "investment", Color: "#8BC34A")
        };

        using var trans = conn.BeginTransaction();
        foreach (var cat in defaultCategories)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = @"
                INSERT INTO Categories (Id, UserId, Name, Type, Icon, Color, IsDefault, CreatedAt)
                VALUES (@id, @userId, @name, @type, @icon, @color, 1, @createdAt);
            ";
            cmd.AddWithValue("@id", Guid.NewGuid().ToString());
            cmd.AddWithValue("@userId", userId);
            cmd.AddWithValue("@name", cat.Name);
            cmd.AddWithValue("@type", cat.Type);
            cmd.AddWithValue("@icon", cat.Icon);
            cmd.AddWithValue("@color", cat.Color);
            cmd.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        trans.Commit();
    }

    private const string SqliteSchema = @"
        CREATE TABLE IF NOT EXISTS Users (
            Id TEXT PRIMARY KEY,
            FullName TEXT NOT NULL,
            Email TEXT NOT NULL UNIQUE COLLATE NOCASE,
            PasswordHash TEXT NOT NULL,
            PasswordSalt TEXT NOT NULL,
            PhoneNumber TEXT,
            Currency TEXT NOT NULL DEFAULT 'EGP',
            Theme TEXT NOT NULL DEFAULT 'light',
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Sessions (
            Token TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Categories (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            Type TEXT NOT NULL,
            Icon TEXT NOT NULL,
            Color TEXT NOT NULL,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Transactions (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            CategoryId TEXT NOT NULL,
            Title TEXT NOT NULL,
            Amount REAL NOT NULL,
            Type TEXT NOT NULL,
            Date TEXT NOT NULL,
            Notes TEXT,
            CreatedAt TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
            FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS Budgets (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            CategoryId TEXT NOT NULL,
            LimitAmount REAL NOT NULL,
            Period TEXT NOT NULL DEFAULT 'monthly',
            StartDate TEXT,
            EndDate TEXT,
            CreatedAt TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
            FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS SavingsGoals (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            Name TEXT NOT NULL,
            TargetAmount REAL NOT NULL,
            CurrentAmount REAL NOT NULL DEFAULT 0,
            TargetDate TEXT,
            Description TEXT,
            CreatedAt TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS SavingsTransactions (
            Id TEXT PRIMARY KEY,
            GoalId TEXT NOT NULL,
            UserId TEXT NOT NULL,
            Type TEXT NOT NULL,
            Amount REAL NOT NULL,
            Date TEXT NOT NULL,
            Notes TEXT,
            CreatedAt TEXT NOT NULL,
            FOREIGN KEY (GoalId) REFERENCES SavingsGoals(Id) ON DELETE CASCADE,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS idx_sessions_token ON Sessions(Token);
        CREATE INDEX IF NOT EXISTS idx_categories_user ON Categories(UserId);
        CREATE INDEX IF NOT EXISTS idx_transactions_user ON Transactions(UserId);
        CREATE INDEX IF NOT EXISTS idx_transactions_date ON Transactions(Date);
        CREATE INDEX IF NOT EXISTS idx_budgets_user ON Budgets(UserId);
        CREATE INDEX IF NOT EXISTS idx_savings_user ON SavingsGoals(UserId);
    ";

    private const string PostgresSchema = @"
        CREATE TABLE IF NOT EXISTS Users (
            Id TEXT PRIMARY KEY,
            FullName TEXT NOT NULL,
            Email TEXT NOT NULL,
            PasswordHash TEXT NOT NULL,
            PasswordSalt TEXT NOT NULL,
            PhoneNumber TEXT,
            Currency TEXT NOT NULL DEFAULT 'EGP',
            Theme TEXT NOT NULL DEFAULT 'light',
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Sessions (
            Token TEXT PRIMARY KEY,
            UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            ExpiresAt TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Categories (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            Name TEXT NOT NULL,
            Type TEXT NOT NULL,
            Icon TEXT NOT NULL,
            Color TEXT NOT NULL,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Transactions (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            CategoryId TEXT NOT NULL REFERENCES Categories(Id) ON DELETE RESTRICT,
            Title TEXT NOT NULL,
            Amount DOUBLE PRECISION NOT NULL,
            Type TEXT NOT NULL,
            Date TEXT NOT NULL,
            Notes TEXT,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Budgets (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            CategoryId TEXT NOT NULL REFERENCES Categories(Id) ON DELETE CASCADE,
            LimitAmount DOUBLE PRECISION NOT NULL,
            Period TEXT NOT NULL DEFAULT 'monthly',
            StartDate TEXT,
            EndDate TEXT,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS SavingsGoals (
            Id TEXT PRIMARY KEY,
            UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            Name TEXT NOT NULL,
            TargetAmount DOUBLE PRECISION NOT NULL,
            CurrentAmount DOUBLE PRECISION NOT NULL DEFAULT 0,
            TargetDate TEXT,
            Description TEXT,
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS SavingsTransactions (
            Id TEXT PRIMARY KEY,
            GoalId TEXT NOT NULL REFERENCES SavingsGoals(Id) ON DELETE CASCADE,
            UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            Type TEXT NOT NULL,
            Amount DOUBLE PRECISION NOT NULL,
            Date TEXT NOT NULL,
            Notes TEXT,
            CreatedAt TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_lower ON Users(LOWER(Email));
        CREATE INDEX IF NOT EXISTS idx_sessions_token ON Sessions(Token);
        CREATE INDEX IF NOT EXISTS idx_categories_user ON Categories(UserId);
        CREATE INDEX IF NOT EXISTS idx_transactions_user ON Transactions(UserId);
        CREATE INDEX IF NOT EXISTS idx_transactions_date ON Transactions(Date);
        CREATE INDEX IF NOT EXISTS idx_budgets_user ON Budgets(UserId);
        CREATE INDEX IF NOT EXISTS idx_savings_user ON SavingsGoals(UserId);
    ";
}

public static class DatabaseCommandExtensions
{
    public static DbParameter AddWithValue(this DbCommand command, string parameterName, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
        return parameter;
    }
}
