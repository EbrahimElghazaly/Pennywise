using System.Data.Common;
using Pennywise.Data;
using Pennywise.Models;

namespace Pennywise.Repositories;

public class UserRepository
{
    private readonly DatabaseContext _db;

    public UserRepository(DatabaseContext db)
    {
        _db = db;
    }

    public User? GetUserById(string id)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, FullName, Email, PasswordHash, PasswordSalt, PhoneNumber, Currency, Theme, CreatedAt
            FROM Users WHERE Id = @id;
        ";
        cmd.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return MapUser(reader);
        }
        return null;
    }

    public User? GetUserByEmail(string email)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        var emailMatch = _db.UsesPostgres
            ? "LOWER(Email) = LOWER(@email)"
            : "Email = @email COLLATE NOCASE";
        cmd.CommandText = $@"
            SELECT Id, FullName, Email, PasswordHash, PasswordSalt, PhoneNumber, Currency, Theme, CreatedAt
            FROM Users WHERE {emailMatch};
        ";
        cmd.AddWithValue("@email", email);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return MapUser(reader);
        }
        return null;
    }

    public void CreateUser(User user)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Users (Id, FullName, Email, PasswordHash, PasswordSalt, PhoneNumber, Currency, Theme, CreatedAt)
            VALUES (@id, @fullName, @email, @hash, @salt, @phone, @currency, @theme, @createdAt);
        ";
        cmd.AddWithValue("@id", user.Id);
        cmd.AddWithValue("@fullName", user.FullName);
        cmd.AddWithValue("@email", user.Email);
        cmd.AddWithValue("@hash", user.PasswordHash);
        cmd.AddWithValue("@salt", user.PasswordSalt);
        cmd.AddWithValue("@phone", (object?)user.PhoneNumber ?? DBNull.Value);
        cmd.AddWithValue("@currency", user.Currency);
        cmd.AddWithValue("@theme", user.Theme);
        cmd.AddWithValue("@createdAt", user.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        _db.SeedDefaultCategoriesForUser(user.Id);
    }

    public void UpdateProfile(string id, string fullName, string email, string? phoneNumber)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Users
            SET FullName = @fullName, Email = @email, PhoneNumber = @phone
            WHERE Id = @id;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@fullName", fullName);
        cmd.AddWithValue("@email", email);
        cmd.AddWithValue("@phone", (object?)phoneNumber ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void UpdatePreferences(string id, string currency, string theme)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Users
            SET Currency = @currency, Theme = @theme
            WHERE Id = @id;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@currency", currency);
        cmd.AddWithValue("@theme", theme);
        cmd.ExecuteNonQuery();
    }

    public void UpdatePassword(string id, string passwordHash, string passwordSalt)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Users
            SET PasswordHash = @hash, PasswordSalt = @salt
            WHERE Id = @id;
        ";
        cmd.AddWithValue("@id", id);
        cmd.AddWithValue("@hash", passwordHash);
        cmd.AddWithValue("@salt", passwordSalt);
        cmd.ExecuteNonQuery();
    }

    public Session CreateSession(string userId, int days = 30)
    {
        var session = new Session
        {
            Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(days)
        };

        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Sessions (Token, UserId, ExpiresAt, CreatedAt)
            VALUES (@token, @userId, @expires, @created);
        ";
        cmd.AddWithValue("@token", session.Token);
        cmd.AddWithValue("@userId", session.UserId);
        cmd.AddWithValue("@expires", session.ExpiresAt.ToString("o"));
        cmd.AddWithValue("@created", session.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        return session;
    }

    public Session? GetSession(string token)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Token, UserId, ExpiresAt, CreatedAt
            FROM Sessions
            WHERE Token = @token AND ExpiresAt > @now;
        ";
        cmd.AddWithValue("@token", token);
        cmd.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Session
            {
                Token = reader.GetString(0),
                UserId = reader.GetString(1),
                ExpiresAt = DateTime.Parse(reader.GetString(2)),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            };
        }
        return null;
    }

    public void DeleteSession(string token)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Sessions WHERE Token = @token;";
        cmd.AddWithValue("@token", token);
        cmd.ExecuteNonQuery();
    }

    public void DeleteUserSessions(string userId)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Sessions WHERE UserId = @userId;";
        cmd.AddWithValue("@userId", userId);
        cmd.ExecuteNonQuery();
    }

    private static User MapUser(DbDataReader reader)
    {
        return new User
        {
            Id = reader.GetString(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            PasswordSalt = reader.GetString(4),
            PhoneNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
            Currency = reader.IsDBNull(6) ? "EGP" : reader.GetString(6),
            Theme = reader.IsDBNull(7) ? "light" : reader.GetString(7),
            CreatedAt = DateTime.Parse(reader.GetString(8))
        };
    }
}
