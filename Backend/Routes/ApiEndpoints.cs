using System.Globalization;
using System.Text.RegularExpressions;
using Pennywise.Models;
using Pennywise.Repositories;
using Pennywise.Services;

namespace Pennywise.Routes;

public static class ApiEndpoints
{
    public static void MapPennywiseApi(this WebApplication app)
    {
        // ---------------- AUTHENTICATION ----------------
        app.MapPost("/api/auth/register", (RegisterRequest req, UserRepository userRepo, HttpResponse res, HttpContext ctx, IWebHostEnvironment environment) =>
        {
            if (string.IsNullOrWhiteSpace(req.FullName))
                return Results.BadRequest(new { success = false, message = "Full name is required." });

            if (string.IsNullOrWhiteSpace(req.Email) || !Regex.IsMatch(req.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return Results.BadRequest(new { success = false, message = "A valid email address is required." });

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                return Results.BadRequest(new { success = false, message = "Password must be at least 6 characters long." });

            if (req.Password != req.ConfirmPassword)
                return Results.BadRequest(new { success = false, message = "Passwords do not match." });

            if (!req.AgreeToTerms)
                return Results.BadRequest(new { success = false, message = "You must agree to the Terms & Conditions." });

            var existing = userRepo.GetUserByEmail(req.Email);
            if (existing != null)
                return Results.BadRequest(new { success = false, message = "An account with this email address already exists." });

            var (hash, salt) = PasswordHasher.HashPassword(req.Password);
            var user = new User
            {
                FullName = req.FullName.Trim(),
                Email = req.Email.Trim().ToLowerInvariant(),
                PasswordHash = hash,
                PasswordSalt = salt,
                Currency = "EGP",
                Theme = "light",
                CreatedAt = DateTime.UtcNow
            };

            userRepo.CreateUser(user);
            var session = userRepo.CreateSession(user.Id, days: 30);

            res.Cookies.Append("pennywise_session", session.Token, CreateSessionCookieOptions(ctx, environment, session.ExpiresAt));

            return Results.Ok(new
            {
                success = true,
                message = "Account created successfully.",
                token = session.Token,
                user = new UserProfileDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Currency, user.Theme, user.CreatedAt.ToString("o"))
            });
        });

        app.MapPost("/api/auth/login", (LoginRequest req, UserRepository userRepo, HttpResponse res, HttpContext ctx, IWebHostEnvironment environment) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { success = false, message = "Email and password are required." });

            var user = userRepo.GetUserByEmail(req.Email);
            if (user == null || !PasswordHasher.VerifyPassword(req.Password, user.PasswordHash, user.PasswordSalt))
            {
                return Results.BadRequest(new { success = false, message = "Invalid email or password. Please try again." });
            }

            int days = req.RememberMe ? 60 : 7;
            var session = userRepo.CreateSession(user.Id, days);

            res.Cookies.Append("pennywise_session", session.Token, CreateSessionCookieOptions(ctx, environment, session.ExpiresAt));

            return Results.Ok(new
            {
                success = true,
                message = "Login successful.",
                token = session.Token,
                user = new UserProfileDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Currency, user.Theme, user.CreatedAt.ToString("o"))
            });
        });

        app.MapPost("/api/auth/logout", (HttpContext ctx, UserRepository userRepo, HttpResponse res) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (ctx.Request.Cookies.TryGetValue("pennywise_session", out var cookieToken))
            {
                userRepo.DeleteSession(cookieToken);
            }
            if (ctx.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var h = authHeader.ToString();
                if (h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    userRepo.DeleteSession(h[7..].Trim());
                }
            }

            res.Cookies.Delete("pennywise_session");
            return Results.Ok(new { success = true, message = "Logged out successfully." });
        });

        app.MapGet("/api/auth/me", (HttpContext ctx, UserRepository userRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null)
                return Results.Unauthorized();

            return Results.Ok(new
            {
                success = true,
                user = new UserProfileDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Currency, user.Theme, user.CreatedAt.ToString("o"))
            });
        });

        app.MapPost("/api/auth/change-password", (ChangePasswordRequest req, HttpContext ctx, UserRepository userRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.CurrentPassword))
                return Results.BadRequest(new { success = false, message = "Current password is required." });

            if (!PasswordHasher.VerifyPassword(req.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                return Results.BadRequest(new { success = false, message = "Current password is incorrect." });

            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
                return Results.BadRequest(new { success = false, message = "New password must be at least 6 characters long." });

            if (req.NewPassword != req.ConfirmNewPassword)
                return Results.BadRequest(new { success = false, message = "New passwords do not match." });

            var (newHash, newSalt) = PasswordHasher.HashPassword(req.NewPassword);
            userRepo.UpdatePassword(user.Id, newHash, newSalt);

            return Results.Ok(new { success = true, message = "Password updated successfully." });
        });

        // ---------------- DASHBOARD ----------------
        app.MapGet("/api/dashboard/summary", (HttpContext ctx, UserRepository userRepo, AnalyticsService analytics) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var summary = analytics.GetDashboardSummary(user.Id, user.Currency);
            return Results.Ok(new { success = true, data = summary });
        });

        app.MapGet("/api/dashboard/charts", (HttpContext ctx, UserRepository userRepo, AnalyticsService analytics) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var charts = analytics.GetDashboardCharts(user.Id);
            return Results.Ok(new { success = true, data = charts });
        });

        app.MapGet("/api/dashboard/recent-transactions", (HttpContext ctx, UserRepository userRepo, TransactionRepository txRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var (items, totalCount, _, _) = txRepo.GetTransactions(user.Id, pageSize: 5, sortBy: "date", sortOrder: "desc");
            return Results.Ok(new { success = true, data = items });
        });

        // ---------------- TRANSACTIONS ----------------
        app.MapGet("/api/transactions", (
            HttpContext ctx,
            UserRepository userRepo,
            TransactionRepository txRepo,
            string? type,
            string? categoryId,
            string? startDate,
            string? endDate,
            string? search,
            int? page,
            int? pageSize,
            string? sortBy,
            string? sortOrder) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            int p = page.GetValueOrDefault(1);
            int ps = pageSize.GetValueOrDefault(10);
            if (p < 1) p = 1;
            if (ps < 1 || ps > 100) ps = 10;

            var (items, totalCount, totalIncome, totalExpenses) = txRepo.GetTransactions(
                user.Id,
                type: type,
                categoryId: categoryId,
                startDate: startDate,
                endDate: endDate,
                search: search,
                page: p,
                pageSize: ps,
                sortBy: sortBy ?? "date",
                sortOrder: sortOrder ?? "desc");

            int totalPages = (int)Math.Ceiling((double)totalCount / ps);

            return Results.Ok(new
            {
                success = true,
                data = items,
                pagination = new
                {
                    currentPage = p,
                    pageSize = ps,
                    totalCount = totalCount,
                    totalPages = Math.Max(1, totalPages)
                },
                totals = new
                {
                    totalIncome,
                    totalExpenses,
                    netCashFlow = totalIncome - totalExpenses,
                    currency = user.Currency
                }
            });
        });

        app.MapGet("/api/transactions/{id}", (string id, HttpContext ctx, UserRepository userRepo, TransactionRepository txRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var tx = txRepo.GetTransactionById(user.Id, id);
            if (tx == null) return Results.NotFound(new { success = false, message = "Transaction not found." });

            return Results.Ok(new { success = true, data = tx });
        });

        app.MapPost("/api/transactions", (TransactionDto dto, HttpContext ctx, UserRepository userRepo, TransactionRepository txRepo, CategoryRepository catRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Title))
                return Results.BadRequest(new { success = false, message = "Transaction title is required." });

            if (dto.Amount <= 0)
                return Results.BadRequest(new { success = false, message = "Transaction amount must be greater than zero." });

            if (!IsTransactionType(dto.Type))
                return Results.BadRequest(new { success = false, message = "Transaction type must be income or expense." });

            if (!IsValidOptionalDate(dto.Date))
                return Results.BadRequest(new { success = false, message = "Transaction date must use YYYY-MM-DD format." });

            var cat = catRepo.GetCategoryById(user.Id, dto.CategoryId);
            if (cat == null)
                return Results.BadRequest(new { success = false, message = "Selected category does not exist." });

            if (!string.Equals(cat.Type, dto.Type, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { success = false, message = "Transaction type must match the selected category." });

            var created = txRepo.CreateTransaction(user.Id, dto);
            return Results.Ok(new { success = true, message = "Transaction created successfully.", data = created });
        });

        app.MapPut("/api/transactions/{id}", (string id, TransactionDto dto, HttpContext ctx, UserRepository userRepo, TransactionRepository txRepo, CategoryRepository catRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Title))
                return Results.BadRequest(new { success = false, message = "Transaction title is required." });

            if (dto.Amount <= 0)
                return Results.BadRequest(new { success = false, message = "Transaction amount must be greater than zero." });

            if (!IsTransactionType(dto.Type))
                return Results.BadRequest(new { success = false, message = "Transaction type must be income or expense." });

            if (string.IsNullOrWhiteSpace(dto.Date) || !IsValidOptionalDate(dto.Date))
                return Results.BadRequest(new { success = false, message = "A valid transaction date in YYYY-MM-DD format is required." });

            var cat = catRepo.GetCategoryById(user.Id, dto.CategoryId);
            if (cat == null)
                return Results.BadRequest(new { success = false, message = "Selected category does not exist." });

            if (!string.Equals(cat.Type, dto.Type, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { success = false, message = "Transaction type must match the selected category." });

            bool ok = txRepo.UpdateTransaction(user.Id, id, dto);
            if (!ok) return Results.NotFound(new { success = false, message = "Transaction not found." });

            var updated = txRepo.GetTransactionById(user.Id, id);
            return Results.Ok(new { success = true, message = "Transaction updated successfully.", data = updated });
        });

        app.MapDelete("/api/transactions/{id}", (string id, HttpContext ctx, UserRepository userRepo, TransactionRepository txRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            bool ok = txRepo.DeleteTransaction(user.Id, id);
            if (!ok) return Results.NotFound(new { success = false, message = "Transaction not found." });

            return Results.Ok(new { success = true, message = "Transaction deleted successfully." });
        });

        // ---------------- CATEGORIES ----------------
        app.MapGet("/api/categories", (HttpContext ctx, UserRepository userRepo, CategoryRepository catRepo, string? type) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var categories = catRepo.GetCategories(user.Id, type);
            return Results.Ok(new { success = true, data = categories });
        });

        app.MapGet("/api/categories/{id}", (string id, HttpContext ctx, UserRepository userRepo, CategoryRepository catRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var cat = catRepo.GetCategoryById(user.Id, id);
            if (cat == null) return Results.NotFound(new { success = false, message = "Category not found." });

            return Results.Ok(new { success = true, data = cat });
        });

        app.MapPost("/api/categories", (CategoryDto dto, HttpContext ctx, UserRepository userRepo, CategoryRepository catRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest(new { success = false, message = "Category name is required." });

            if (!IsCategoryType(dto.Type) || !IsValidColor(dto.Color))
                return Results.BadRequest(new { success = false, message = "Category type must be income or expense and color must be a six-digit hex value." });

            var created = catRepo.CreateCategory(user.Id, dto);
            return Results.Ok(new { success = true, message = "Category created successfully.", data = created });
        });

        app.MapPut("/api/categories/{id}", (string id, CategoryDto dto, HttpContext ctx, UserRepository userRepo, CategoryRepository catRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest(new { success = false, message = "Category name is required." });

            if (!IsCategoryType(dto.Type) || !IsValidColor(dto.Color))
                return Results.BadRequest(new { success = false, message = "Category type must be income or expense and color must be a six-digit hex value." });

            var existing = catRepo.GetCategoryById(user.Id, id);
            if (existing == null) return Results.NotFound(new { success = false, message = "Category not found." });

            if (!string.Equals(existing.Type, dto.Type, StringComparison.OrdinalIgnoreCase)
                && (existing.TransactionCount > 0 || catRepo.HasBudgets(user.Id, id)))
            {
                return Results.BadRequest(new { success = false, message = "A category with transactions or budgets cannot change its type." });
            }

            bool ok = catRepo.UpdateCategory(user.Id, id, dto);
            if (!ok) return Results.NotFound(new { success = false, message = "Category not found." });

            var updated = catRepo.GetCategoryById(user.Id, id);
            return Results.Ok(new { success = true, message = "Category updated successfully.", data = updated });
        });

        app.MapDelete("/api/categories/{id}", (string id, HttpContext ctx, UserRepository userRepo, CategoryRepository catRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var (success, msg) = catRepo.DeleteCategory(user.Id, id);
            if (!success)
                return Results.BadRequest(new { success = false, message = msg });

            return Results.Ok(new { success = true, message = msg });
        });

        // ---------------- BUDGETS ----------------
        app.MapGet("/api/budgets", (HttpContext ctx, UserRepository userRepo, BudgetRepository budgetRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var budgets = budgetRepo.GetBudgets(user.Id);
            return Results.Ok(new { success = true, data = budgets });
        });

        app.MapGet("/api/budgets/summary", (HttpContext ctx, UserRepository userRepo, BudgetRepository budgetRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var (totalBudget, totalSpent, remaining, percentage) = budgetRepo.GetMonthlySummary(user.Id);
            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    totalBudget,
                    totalSpent,
                    remaining,
                    percentage,
                    currency = user.Currency
                }
            });
        });

        app.MapGet("/api/budgets/{id}", (string id, HttpContext ctx, UserRepository userRepo, BudgetRepository budgetRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var b = budgetRepo.GetBudgetById(user.Id, id);
            if (b == null) return Results.NotFound(new { success = false, message = "Budget not found." });

            return Results.Ok(new { success = true, data = b });
        });

        app.MapPost("/api/budgets", (BudgetDto dto, HttpContext ctx, UserRepository userRepo, BudgetRepository budgetRepo, CategoryRepository catRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (dto.LimitAmount <= 0)
                return Results.BadRequest(new { success = false, message = "Budget limit must be greater than zero." });

            if (!IsBudgetPeriod(dto.Period) || !IsValidDateRange(dto.StartDate, dto.EndDate))
                return Results.BadRequest(new { success = false, message = "Budget period or date range is invalid." });

            var cat = catRepo.GetCategoryById(user.Id, dto.CategoryId);
            if (cat == null)
                return Results.BadRequest(new { success = false, message = "Selected category does not exist." });

            if (!string.Equals(cat.Type, "expense", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { success = false, message = "Budgets can only be assigned to expense categories." });

            var created = budgetRepo.CreateBudget(user.Id, dto);
            return Results.Ok(new { success = true, message = "Budget created successfully.", data = created });
        });

        app.MapPut("/api/budgets/{id}", (string id, BudgetDto dto, HttpContext ctx, UserRepository userRepo, BudgetRepository budgetRepo, CategoryRepository catRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (dto.LimitAmount <= 0)
                return Results.BadRequest(new { success = false, message = "Budget limit must be greater than zero." });

            if (!IsBudgetPeriod(dto.Period) || !IsValidDateRange(dto.StartDate, dto.EndDate))
                return Results.BadRequest(new { success = false, message = "Budget period or date range is invalid." });

            var cat = catRepo.GetCategoryById(user.Id, dto.CategoryId);
            if (cat == null)
                return Results.BadRequest(new { success = false, message = "Selected category does not exist." });

            if (!string.Equals(cat.Type, "expense", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { success = false, message = "Budgets can only be assigned to expense categories." });

            bool ok = budgetRepo.UpdateBudget(user.Id, id, dto);
            if (!ok) return Results.NotFound(new { success = false, message = "Budget not found." });

            var updated = budgetRepo.GetBudgetById(user.Id, id);
            return Results.Ok(new { success = true, message = "Budget updated successfully.", data = updated });
        });

        app.MapDelete("/api/budgets/{id}", (string id, HttpContext ctx, UserRepository userRepo, BudgetRepository budgetRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            bool ok = budgetRepo.DeleteBudget(user.Id, id);
            if (!ok) return Results.NotFound(new { success = false, message = "Budget not found." });

            return Results.Ok(new { success = true, message = "Budget deleted successfully." });
        });

        // ---------------- REPORTS ----------------
        app.MapGet("/api/reports/summary", (HttpContext ctx, UserRepository userRepo, AnalyticsService analytics, string? startDate, string? endDate) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (!IsValidDateRange(startDate, endDate))
                return Results.BadRequest(new { success = false, message = "Report dates must use YYYY-MM-DD format and startDate cannot follow endDate." });

            var summary = analytics.GetReportsSummary(user.Id, startDate, endDate, user.Currency);
            return Results.Ok(new { success = true, data = summary });
        });

        app.MapGet("/api/reports/expenses-by-category", (HttpContext ctx, UserRepository userRepo, AnalyticsService analytics, string? startDate, string? endDate) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (!IsValidDateRange(startDate, endDate))
                return Results.BadRequest(new { success = false, message = "Report dates must use YYYY-MM-DD format and startDate cannot follow endDate." });

            var list = analytics.GetExpensesByCategory(user.Id, startDate, endDate);
            return Results.Ok(new { success = true, data = list });
        });

        app.MapGet("/api/reports/monthly-comparison", (HttpContext ctx, UserRepository userRepo, AnalyticsService analytics) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var list = analytics.GetMonthlyComparison(user.Id);
            return Results.Ok(new { success = true, data = list });
        });

        app.MapGet("/api/reports/top-category", (HttpContext ctx, UserRepository userRepo, AnalyticsService analytics, string? startDate, string? endDate) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (!IsValidDateRange(startDate, endDate))
                return Results.BadRequest(new { success = false, message = "Report dates must use YYYY-MM-DD format and startDate cannot follow endDate." });

            var top = analytics.GetTopCategory(user.Id, startDate, endDate);
            return Results.Ok(new { success = true, data = top });
        });

        app.MapGet("/api/reports/export-csv", (HttpContext ctx, UserRepository userRepo, AnalyticsService analytics, string? startDate, string? endDate) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (!IsValidDateRange(startDate, endDate))
                return Results.BadRequest(new { success = false, message = "Report dates must use YYYY-MM-DD format and startDate cannot follow endDate." });

            var csv = analytics.GenerateCsvExport(user.Id, startDate, endDate);
            var filename = $"pennywise-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", filename);
        });

        // ---------------- SAVINGS GOALS ----------------
        app.MapGet("/api/savings-goals", (HttpContext ctx, UserRepository userRepo, SavingsGoalRepository savingsRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var goals = savingsRepo.GetGoals(user.Id);
            decimal totalTarget = goals.Sum(g => g.TargetAmount);
            decimal totalSaved = goals.Sum(g => g.CurrentAmount);
            decimal remaining = Math.Max(0, totalTarget - totalSaved);

            return Results.Ok(new
            {
                success = true,
                data = goals,
                summary = new
                {
                    totalGoals = goals.Count,
                    totalTargetAmount = totalTarget,
                    totalSaved = totalSaved,
                    remainingAmount = remaining,
                    currency = user.Currency
                }
            });
        });

        app.MapGet("/api/savings-goals/{id}", (string id, HttpContext ctx, UserRepository userRepo, SavingsGoalRepository savingsRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            var goal = savingsRepo.GetGoalById(user.Id, id);
            if (goal == null) return Results.NotFound(new { success = false, message = "Savings goal not found." });

            var transactions = savingsRepo.GetGoalTransactions(user.Id, id);

            return Results.Ok(new
            {
                success = true,
                data = goal,
                transactions = transactions
            });
        });

        app.MapPost("/api/savings-goals", (SavingsGoalDto dto, HttpContext ctx, UserRepository userRepo, SavingsGoalRepository savingsRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest(new { success = false, message = "Goal name is required." });

            if (dto.TargetAmount <= 0)
                return Results.BadRequest(new { success = false, message = "Target amount must be greater than zero." });

            if (dto.CurrentAmount < 0 || !IsValidOptionalDate(dto.TargetDate))
                return Results.BadRequest(new { success = false, message = "Current amount cannot be negative and target date must use YYYY-MM-DD format." });

            var created = savingsRepo.CreateGoal(user.Id, dto);
            return Results.Ok(new { success = true, message = "Savings goal created successfully.", data = created });
        });

        app.MapPut("/api/savings-goals/{id}", (string id, SavingsGoalDto dto, HttpContext ctx, UserRepository userRepo, SavingsGoalRepository savingsRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest(new { success = false, message = "Goal name is required." });

            if (dto.TargetAmount <= 0)
                return Results.BadRequest(new { success = false, message = "Target amount must be greater than zero." });

            if (dto.CurrentAmount < 0 || !IsValidOptionalDate(dto.TargetDate))
                return Results.BadRequest(new { success = false, message = "Current amount cannot be negative and target date must use YYYY-MM-DD format." });

            bool ok = savingsRepo.UpdateGoal(user.Id, id, dto);
            if (!ok) return Results.NotFound(new { success = false, message = "Savings goal not found." });

            var updated = savingsRepo.GetGoalById(user.Id, id);
            return Results.Ok(new { success = true, message = "Savings goal updated successfully.", data = updated });
        });

        app.MapDelete("/api/savings-goals/{id}", (string id, HttpContext ctx, UserRepository userRepo, SavingsGoalRepository savingsRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            bool ok = savingsRepo.DeleteGoal(user.Id, id);
            if (!ok) return Results.NotFound(new { success = false, message = "Savings goal not found." });

            return Results.Ok(new { success = true, message = "Savings goal deleted successfully." });
        });

        app.MapPost("/api/savings-goals/{id}/contributions", (string id, SavingsContributionDto dto, HttpContext ctx, UserRepository userRepo, SavingsGoalRepository savingsRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (!IsValidOptionalDate(dto.Date))
                return Results.BadRequest(new { success = false, message = "Savings date must use YYYY-MM-DD format." });

            var (success, msg, goal) = savingsRepo.AddContribution(user.Id, id, dto.Amount, dto.Date, dto.Notes);
            if (!success)
                return Results.BadRequest(new { success = false, message = msg });

            return Results.Ok(new { success = true, message = msg, data = goal });
        });

        app.MapPost("/api/savings-goals/{id}/withdrawals", (string id, SavingsContributionDto dto, HttpContext ctx, UserRepository userRepo, SavingsGoalRepository savingsRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (!IsValidOptionalDate(dto.Date))
                return Results.BadRequest(new { success = false, message = "Savings date must use YYYY-MM-DD format." });

            var (success, msg, goal) = savingsRepo.AddWithdrawal(user.Id, id, dto.Amount, dto.Date, dto.Notes);
            if (!success)
                return Results.BadRequest(new { success = false, message = msg });

            return Results.Ok(new { success = true, message = msg, data = goal });
        });

        // ---------------- PROFILE & PREFERENCES ----------------
        app.MapGet("/api/profile", (HttpContext ctx, UserRepository userRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            return Results.Ok(new
            {
                success = true,
                data = new UserProfileDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Currency, user.Theme, user.CreatedAt.ToString("o"))
            });
        });

        app.MapPut("/api/profile", (ProfileUpdateRequest req, HttpContext ctx, UserRepository userRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.FullName))
                return Results.BadRequest(new { success = false, message = "Full name is required." });

            if (string.IsNullOrWhiteSpace(req.Email) || !Regex.IsMatch(req.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return Results.BadRequest(new { success = false, message = "Valid email is required." });

            var existing = userRepo.GetUserByEmail(req.Email);
            if (existing != null && existing.Id != user.Id)
                return Results.BadRequest(new { success = false, message = "Email is already taken by another account." });

            userRepo.UpdateProfile(user.Id, req.FullName.Trim(), req.Email.Trim().ToLowerInvariant(), req.PhoneNumber?.Trim());
            var updated = userRepo.GetUserById(user.Id)!;

            return Results.Ok(new
            {
                success = true,
                message = "Profile updated successfully.",
                data = new UserProfileDto(updated.Id, updated.FullName, updated.Email, updated.PhoneNumber, updated.Currency, updated.Theme, updated.CreatedAt.ToString("o"))
            });
        });

        app.MapGet("/api/preferences", (HttpContext ctx, UserRepository userRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    currency = user.Currency,
                    theme = user.Theme
                }
            });
        });

        app.MapPut("/api/preferences", (PreferencesUpdateRequest req, HttpContext ctx, UserRepository userRepo) =>
        {
            var user = AuthHelper.GetCurrentUser(ctx, userRepo);
            if (user == null) return Results.Unauthorized();

            string currency = req.Currency?.Trim().ToUpperInvariant() ?? "";
            string theme = req.Theme?.Trim().ToLowerInvariant() ?? "";
            if (!IsSupportedCurrency(currency) || (theme != "light" && theme != "dark"))
                return Results.BadRequest(new { success = false, message = "Choose a supported currency and either the light or dark theme." });

            userRepo.UpdatePreferences(user.Id, currency, theme);
            return Results.Ok(new
            {
                success = true,
                message = "Preferences saved successfully.",
                data = new { currency, theme }
            });
        });
    }

    private static bool IsTransactionType(string? type) =>
        string.Equals(type, "income", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "expense", StringComparison.OrdinalIgnoreCase);

    private static bool IsCategoryType(string? type) => IsTransactionType(type);

    private static bool IsBudgetPeriod(string? period) =>
        string.Equals(period, "weekly", StringComparison.OrdinalIgnoreCase)
        || string.Equals(period, "monthly", StringComparison.OrdinalIgnoreCase)
        || string.Equals(period, "yearly", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidOptionalDate(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool IsValidDateRange(string? startDate, string? endDate)
    {
        if (!IsValidOptionalDate(startDate) || !IsValidOptionalDate(endDate))
            return false;

        if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
            return true;

        return DateOnly.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            <= DateOnly.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static bool IsValidColor(string? color) =>
        string.IsNullOrWhiteSpace(color) || Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$");

    private static bool IsSupportedCurrency(string? currency) =>
        currency is "EGP" or "USD" or "EUR" or "GBP" or "SAR" or "AED";

    private static CookieOptions CreateSessionCookieOptions(
        HttpContext context,
        IWebHostEnvironment environment,
        DateTime expiresAt)
    {
        var secureOverride = Environment.GetEnvironmentVariable("PENNYWISE_COOKIE_SECURE");
        var secure = bool.TryParse(secureOverride, out var configuredSecure)
            ? configuredSecure
            : environment.IsProduction() || context.Request.IsHttps;

        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = secure,
            Path = "/",
            Expires = expiresAt
        };
    }
}
