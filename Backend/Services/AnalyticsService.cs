using System.Globalization;
using Pennywise.Data;
using Pennywise.Models;

namespace Pennywise.Services;

public class AnalyticsService
{
    private readonly DatabaseContext _db;

    public AnalyticsService(DatabaseContext db)
    {
        _db = db;
    }

    public DashboardSummaryDto GetDashboardSummary(string userId, string currency)
    {
        using var conn = _db.CreateConnection();

        var now = DateTime.UtcNow;
        var curStart = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
        var curEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)).ToString("yyyy-MM-dd");

        var prevMonthDate = now.AddMonths(-1);
        var prevStart = new DateTime(prevMonthDate.Year, prevMonthDate.Month, 1).ToString("yyyy-MM-dd");
        var prevEnd = new DateTime(prevMonthDate.Year, prevMonthDate.Month, DateTime.DaysInMonth(prevMonthDate.Year, prevMonthDate.Month)).ToString("yyyy-MM-dd");

        decimal curIncome = 0;
        decimal curExpenses = 0;
        decimal prevIncome = 0;
        decimal prevExpenses = 0;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    COALESCE(SUM(CASE WHEN Type = 'income' AND Date >= @curStart AND Date <= @curEnd THEN Amount ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN Type = 'expense' AND Date >= @curStart AND Date <= @curEnd THEN Amount ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN Type = 'income' AND Date >= @prevStart AND Date <= @prevEnd THEN Amount ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN Type = 'expense' AND Date >= @prevStart AND Date <= @prevEnd THEN Amount ELSE 0 END), 0)
                FROM Transactions
                WHERE UserId = @userId;
            ";
            cmd.AddWithValue("@userId", userId);
            cmd.AddWithValue("@curStart", curStart);
            cmd.AddWithValue("@curEnd", curEnd);
            cmd.AddWithValue("@prevStart", prevStart);
            cmd.AddWithValue("@prevEnd", prevEnd);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                curIncome = Convert.ToDecimal(reader.GetDouble(0));
                curExpenses = Convert.ToDecimal(reader.GetDouble(1));
                prevIncome = Convert.ToDecimal(reader.GetDouble(2));
                prevExpenses = Convert.ToDecimal(reader.GetDouble(3));
            }
        }

        // Available balance = current month net or overall net cash flow
        // "Total Income minus Total Expenses"
        decimal availableBalance = curIncome - curExpenses;

        // Savings percentage across all active goals
        decimal totalTarget = 0;
        decimal totalSaved = 0;
        int totalGoals = 0;
        int completedGoals = 0;

        using (var goalCmd = conn.CreateCommand())
        {
            goalCmd.CommandText = @"
                SELECT TargetAmount, CurrentAmount
                FROM SavingsGoals
                WHERE UserId = @userId;
            ";
            goalCmd.AddWithValue("@userId", userId);
            using var reader = goalCmd.ExecuteReader();
            while (reader.Read())
            {
                totalGoals++;
                var tgt = Convert.ToDecimal(reader.GetDouble(0));
                var cur = Convert.ToDecimal(reader.GetDouble(1));
                totalTarget += tgt;
                totalSaved += cur;
                if (cur >= tgt && tgt > 0) completedGoals++;
            }
        }

        decimal savingsPercentage = totalTarget > 0 ? Math.Round((totalSaved / totalTarget) * 100m, 1) : 0m;

        decimal incomeChange = prevIncome > 0 ? Math.Round(((curIncome - prevIncome) / prevIncome) * 100m, 1) : (curIncome > 0 ? 100m : 0m);
        decimal expensesChange = prevExpenses > 0 ? Math.Round(((curExpenses - prevExpenses) / prevExpenses) * 100m, 1) : (curExpenses > 0 ? 100m : 0m);
        decimal prevBalance = prevIncome - prevExpenses;
        decimal balanceChange = prevBalance != 0 ? Math.Round(((availableBalance - prevBalance) / Math.Abs(prevBalance)) * 100m, 1) : (availableBalance > 0 ? 100m : 0m);

        return new DashboardSummaryDto(
            curIncome,
            curExpenses,
            availableBalance,
            savingsPercentage,
            totalGoals,
            completedGoals,
            currency,
            incomeChange,
            expensesChange,
            balanceChange
        );
    }

    public DashboardChartsDto GetDashboardCharts(string userId)
    {
        using var conn = _db.CreateConnection();
        var now = DateTime.UtcNow;

        // Generate last 4-6 months
        var months = new List<DateTime>();
        for (int i = 3; i >= 0; i--)
        {
            months.Add(now.AddMonths(-i));
        }

        var monthlyPoints = new List<MonthlyDataPoint>();
        var trendPoints = new List<SpendingTrendPoint>();

        foreach (var m in months)
        {
            var start = new DateTime(m.Year, m.Month, 1).ToString("yyyy-MM-dd");
            var end = new DateTime(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month)).ToString("yyyy-MM-dd");
            var monthName = m.ToString("MMM");

            decimal income = 0;
            decimal expenses = 0;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    COALESCE(SUM(CASE WHEN Type = 'income' THEN Amount ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN Type = 'expense' THEN Amount ELSE 0 END), 0)
                FROM Transactions
                WHERE UserId = @userId AND Date >= @start AND Date <= @end;
            ";
            cmd.AddWithValue("@userId", userId);
            cmd.AddWithValue("@start", start);
            cmd.AddWithValue("@end", end);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                income = Convert.ToDecimal(reader.GetDouble(0));
                expenses = Convert.ToDecimal(reader.GetDouble(1));
            }

            monthlyPoints.Add(new MonthlyDataPoint(monthName, income, expenses));
            trendPoints.Add(new SpendingTrendPoint(monthName, expenses));
        }

        // Expenses by category (current month or recent 30 days)
        var curStart = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
        var curEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)).ToString("yyyy-MM-dd");

        var categoryExpenses = new List<CategoryExpenseData>();
        decimal totalCatExpenses = 0;

        using (var catCmd = conn.CreateCommand())
        {
            catCmd.CommandText = @"
                SELECT c.Name, c.Color, c.Icon, COALESCE(SUM(t.Amount), 0) as TotalSpent
                FROM Transactions t
                JOIN Categories c ON t.CategoryId = c.Id
                WHERE t.UserId = @userId AND t.Type = 'expense' AND t.Date >= @curStart AND t.Date <= @curEnd
                GROUP BY c.Id, c.Name, c.Color, c.Icon
                ORDER BY TotalSpent DESC;
            ";
            catCmd.AddWithValue("@userId", userId);
            catCmd.AddWithValue("@curStart", curStart);
            catCmd.AddWithValue("@curEnd", curEnd);

            var temp = new List<(string Name, string Color, string Icon, decimal Amount)>();
            using var reader = catCmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var color = reader.GetString(1);
                var icon = reader.GetString(2);
                var amt = Convert.ToDecimal(reader.GetDouble(3));
                totalCatExpenses += amt;
                temp.Add((name, color, icon, amt));
            }

            foreach (var item in temp)
            {
                decimal pct = totalCatExpenses > 0 ? Math.Round((item.Amount / totalCatExpenses) * 100m, 1) : 0m;
                categoryExpenses.Add(new CategoryExpenseData(item.Name, item.Amount, pct, item.Color, item.Icon));
            }
        }

        return new DashboardChartsDto(monthlyPoints, categoryExpenses, trendPoints);
    }

    public ReportsSummaryDto GetReportsSummary(string userId, string? startDate, string? endDate, string currency)
    {
        using var conn = _db.CreateConnection();
        var now = DateTime.UtcNow;

        DateTime sDate = string.IsNullOrWhiteSpace(startDate) ? new DateTime(now.Year, now.Month, 1) : DateTime.Parse(startDate);
        DateTime eDate = string.IsNullOrWhiteSpace(endDate) ? new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)) : DateTime.Parse(endDate);

        if (eDate < sDate) eDate = sDate;

        string sStr = sDate.ToString("yyyy-MM-dd");
        string eStr = eDate.ToString("yyyy-MM-dd");

        decimal totalIncome = 0;
        decimal totalExpenses = 0;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    COALESCE(SUM(CASE WHEN Type = 'income' THEN Amount ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN Type = 'expense' THEN Amount ELSE 0 END), 0)
                FROM Transactions
                WHERE UserId = @userId AND Date >= @start AND Date <= @end;
            ";
            cmd.AddWithValue("@userId", userId);
            cmd.AddWithValue("@start", sStr);
            cmd.AddWithValue("@end", eStr);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                totalIncome = Convert.ToDecimal(reader.GetDouble(0));
                totalExpenses = Convert.ToDecimal(reader.GetDouble(1));
            }
        }

        decimal netCashFlow = totalIncome - totalExpenses;
        int days = Math.Max(1, (int)(eDate.Date - sDate.Date).TotalDays + 1);
        decimal avgDaily = Math.Round(totalExpenses / days, 2);

        return new ReportsSummaryDto(totalIncome, totalExpenses, netCashFlow, avgDaily, days, sStr, eStr, currency);
    }

    public List<CategoryExpenseData> GetExpensesByCategory(string userId, string? startDate, string? endDate)
    {
        using var conn = _db.CreateConnection();
        var now = DateTime.UtcNow;

        string sStr = string.IsNullOrWhiteSpace(startDate) ? new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd") : startDate;
        string eStr = string.IsNullOrWhiteSpace(endDate) ? new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)).ToString("yyyy-MM-dd") : endDate;

        var result = new List<CategoryExpenseData>();
        decimal total = 0;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT c.Name, c.Color, c.Icon, COALESCE(SUM(t.Amount), 0) as TotalSpent
                FROM Transactions t
                JOIN Categories c ON t.CategoryId = c.Id
                WHERE t.UserId = @userId AND t.Type = 'expense' AND t.Date >= @start AND t.Date <= @end
                GROUP BY c.Id, c.Name, c.Color, c.Icon
                ORDER BY TotalSpent DESC;
            ";
            cmd.AddWithValue("@userId", userId);
            cmd.AddWithValue("@start", sStr);
            cmd.AddWithValue("@end", eStr);

            var temp = new List<(string Name, string Color, string Icon, decimal Amount)>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var color = reader.GetString(1);
                var icon = reader.GetString(2);
                var amt = Convert.ToDecimal(reader.GetDouble(3));
                total += amt;
                temp.Add((name, color, icon, amt));
            }

            foreach (var item in temp)
            {
                decimal pct = total > 0 ? Math.Round((item.Amount / total) * 100m, 1) : 0m;
                result.Add(new CategoryExpenseData(item.Name, item.Amount, pct, item.Color, item.Icon));
            }
        }

        return result;
    }

    public List<MonthlyDataPoint> GetMonthlyComparison(string userId)
    {
        using var conn = _db.CreateConnection();
        var now = DateTime.UtcNow;
        var points = new List<MonthlyDataPoint>();

        for (int i = 5; i >= 0; i--)
        {
            var m = now.AddMonths(-i);
            var start = new DateTime(m.Year, m.Month, 1).ToString("yyyy-MM-dd");
            var end = new DateTime(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month)).ToString("yyyy-MM-dd");
            var monthName = m.ToString("MMM");

            decimal income = 0;
            decimal expenses = 0;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    COALESCE(SUM(CASE WHEN Type = 'income' THEN Amount ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN Type = 'expense' THEN Amount ELSE 0 END), 0)
                FROM Transactions
                WHERE UserId = @userId AND Date >= @start AND Date <= @end;
            ";
            cmd.AddWithValue("@userId", userId);
            cmd.AddWithValue("@start", start);
            cmd.AddWithValue("@end", end);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                income = Convert.ToDecimal(reader.GetDouble(0));
                expenses = Convert.ToDecimal(reader.GetDouble(1));
            }

            points.Add(new MonthlyDataPoint(monthName, income, expenses));
        }

        return points;
    }

    public TopCategoryDto? GetTopCategory(string userId, string? startDate, string? endDate)
    {
        var list = GetExpensesByCategory(userId, startDate, endDate);
        if (list.Count > 0)
        {
            var top = list[0];
            return new TopCategoryDto(top.Category, top.Icon, top.Color, top.Amount, top.Percentage);
        }
        return null;
    }

    public string GenerateCsvExport(string userId, string? startDate, string? endDate)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();

        string query = @"
            SELECT t.Date, t.Title, c.Name AS Category, t.Type, t.Amount, t.Notes
            FROM Transactions t
            JOIN Categories c ON t.CategoryId = c.Id
            WHERE t.UserId = @userId
        ";
        cmd.AddWithValue("@userId", userId);

        if (!string.IsNullOrWhiteSpace(startDate))
        {
            query += " AND t.Date >= @start";
            cmd.AddWithValue("@start", startDate);
        }
        if (!string.IsNullOrWhiteSpace(endDate))
        {
            query += " AND t.Date <= @end";
            cmd.AddWithValue("@end", endDate);
        }

        query += " ORDER BY t.Date DESC, t.CreatedAt DESC;";
        cmd.CommandText = query;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date,Title,Category,Type,Amount,Notes");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string date = reader.GetString(0);
            string title = EscapeCsv(reader.GetString(1));
            string cat = EscapeCsv(reader.GetString(2));
            string type = EscapeCsv(reader.GetString(3));
            decimal amt = Convert.ToDecimal(reader.GetDouble(4));
            string notes = reader.IsDBNull(5) ? "" : EscapeCsv(reader.GetString(5));

            sb.AppendLine($"{date},{title},{cat},{type},{amt.ToString("F2", CultureInfo.InvariantCulture)},{notes}");
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        if (val.Contains(',') || val.Contains('"') || val.Contains('\n') || val.Contains('\r'))
        {
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
        return val;
    }
}
