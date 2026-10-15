namespace Pennywise.Models;

public record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword,
    bool AgreeToTerms
);

public record LoginRequest(
    string Email,
    string Password,
    bool RememberMe
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);

public record ProfileUpdateRequest(
    string FullName,
    string Email,
    string? PhoneNumber
);

public record PreferencesUpdateRequest(
    string Currency,
    string Theme
);

public record CategoryDto(
    string Name,
    string Type,
    string Icon,
    string Color
);

public record TransactionDto(
    string CategoryId,
    string Title,
    decimal Amount,
    string Type,
    string Date,
    string? Notes
);

public record BudgetDto(
    string CategoryId,
    decimal LimitAmount,
    string Period,
    string? StartDate,
    string? EndDate
);

public record SavingsGoalDto(
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    string? TargetDate,
    string? Description
);

public record SavingsContributionDto(
    decimal Amount,
    string? Date,
    string? Notes
);

public record UserProfileDto(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Currency,
    string Theme,
    string CreatedAt
);

public record DashboardSummaryDto(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal AvailableBalance,
    decimal SavingsPercentage,
    int TotalGoals,
    int CompletedGoals,
    string Currency,
    decimal IncomeChangePercent,
    decimal ExpensesChangePercent,
    decimal BalanceChangePercent
);

public record MonthlyDataPoint(
    string Month,
    decimal Income,
    decimal Expenses
);

public record CategoryExpenseData(
    string Category,
    decimal Amount,
    decimal Percentage,
    string Color,
    string Icon
);

public record SpendingTrendPoint(
    string Month,
    decimal Expenses
);

public record DashboardChartsDto(
    List<MonthlyDataPoint> MonthlyComparison,
    List<CategoryExpenseData> ExpensesByCategory,
    List<SpendingTrendPoint> SpendingTrend
);

public record ReportsSummaryDto(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetCashFlow,
    decimal AverageDailySpending,
    int DaysInPeriod,
    string StartDate,
    string EndDate,
    string Currency
);

public record TopCategoryDto(
    string Name,
    string Icon,
    string Color,
    decimal Amount,
    decimal Percentage
);

public record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data = default
);
