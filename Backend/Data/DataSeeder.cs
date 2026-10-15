using Pennywise.Models;
using Pennywise.Repositories;
using Pennywise.Services;

namespace Pennywise.Data;

public static class DataSeeder
{
    public static void SeedDemoUserIfEmpty(
        UserRepository userRepo,
        CategoryRepository catRepo,
        TransactionRepository txRepo,
        BudgetRepository budgetRepo,
        SavingsGoalRepository savingsRepo)
    {
        const string demoEmail = "ahmed@example.com";
        var existing = userRepo.GetUserByEmail(demoEmail);
        if (existing != null) return;

        var currentMonth = DateTime.UtcNow;
        var previousMonth = currentMonth.AddMonths(-1);
        var twoMonthsAgo = currentMonth.AddMonths(-2);
        string MonthDate(DateTime month, int day) =>
            new DateTime(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)))
                .ToString("yyyy-MM-dd");

        var (hash, salt) = PasswordHasher.HashPassword("Password123!");
        var user = new User
        {
            FullName = "Ahmed Mohamed",
            Email = demoEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            PhoneNumber = "+20 123 456 789",
            Currency = "EGP",
            Theme = "light",
            CreatedAt = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        userRepo.CreateUser(user);

        // Fetch user categories
        var categories = catRepo.GetCategories(user.Id);
        var food = categories.FirstOrDefault(c => c.Name == "Food");
        var transport = categories.FirstOrDefault(c => c.Name == "Transportation");
        var shopping = categories.FirstOrDefault(c => c.Name == "Shopping");
        var bills = categories.FirstOrDefault(c => c.Name == "Bills");
        var salary = categories.FirstOrDefault(c => c.Name == "Salary");
        var freelance = categories.FirstOrDefault(c => c.Name == "Freelancing");
        var entertainment = categories.FirstOrDefault(c => c.Name == "Entertainment");

        // Keep demo transactions in the current period so fresh installations show live summaries.
        if (salary != null)
        {
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                salary.Id,
                "Salary",
                12000m,
                "income",
                MonthDate(currentMonth, 10),
                "Monthly salary deposit"
            ));
        }

        if (food != null)
        {
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                food.Id,
                "Grocery Shopping",
                600m,
                "expense",
                MonthDate(currentMonth, 12),
                "Weekly supermarket essentials"
            ));
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                food.Id,
                "Restaurant Dinner",
                900m,
                "expense",
                MonthDate(currentMonth, 6),
                "Dinner with family"
            ));
        }

        if (transport != null)
        {
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                transport.Id,
                "Taxi",
                100m,
                "expense",
                MonthDate(currentMonth, 11),
                "Ride to downtown"
            ));
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                transport.Id,
                "Fuel / Commute",
                600m,
                "expense",
                MonthDate(currentMonth, 4),
                "Car refueling"
            ));
        }

        if (shopping != null)
        {
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                shopping.Id,
                "Online Shopping",
                1500m,
                "expense",
                MonthDate(currentMonth, 7),
                "Headphones and work gear"
            ));
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                shopping.Id,
                "Clothing",
                100m,
                "expense",
                MonthDate(currentMonth, 2),
                "New shirt"
            ));
        }

        if (bills != null)
        {
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                bills.Id,
                "Electricity Bill",
                800m,
                "expense",
                MonthDate(currentMonth, 5),
                "Monthly utility payment"
            ));
        }

        if (entertainment != null)
        {
            txRepo.CreateTransaction(user.Id, new TransactionDto(
                entertainment.Id,
                "Cinema & Entertainment",
                900m,
                "expense",
                MonthDate(currentMonth, 6),
                "Movies and outing"
            ));
        }

        // Prior months for trend/comparison charts.
        if (salary != null && food != null && bills != null && shopping != null && transport != null)
        {
            txRepo.CreateTransaction(user.Id, new TransactionDto(salary.Id, "Salary", 11000m, "income", MonthDate(twoMonthsAgo, 10), "Salary from two months ago"));
            txRepo.CreateTransaction(user.Id, new TransactionDto(food.Id, "Groceries", 1400m, "expense", MonthDate(twoMonthsAgo, 14), null));
            txRepo.CreateTransaction(user.Id, new TransactionDto(bills.Id, "Utilities", 750m, "expense", MonthDate(twoMonthsAgo, 8), null));
            txRepo.CreateTransaction(user.Id, new TransactionDto(shopping.Id, "Summer clothes", 1200m, "expense", MonthDate(twoMonthsAgo, 20), null));

            txRepo.CreateTransaction(user.Id, new TransactionDto(salary.Id, "Salary", 11500m, "income", MonthDate(previousMonth, 10), "Salary from previous month"));
            txRepo.CreateTransaction(user.Id, new TransactionDto(food.Id, "Groceries", 1550m, "expense", MonthDate(previousMonth, 12), null));
            txRepo.CreateTransaction(user.Id, new TransactionDto(transport.Id, "Transport", 650m, "expense", MonthDate(previousMonth, 18), null));
            txRepo.CreateTransaction(user.Id, new TransactionDto(bills.Id, "Bills", 800m, "expense", MonthDate(previousMonth, 5), null));
            txRepo.CreateTransaction(user.Id, new TransactionDto(shopping.Id, "Electronics", 1300m, "expense", MonthDate(previousMonth, 25), null));
        }

        // Seed Budgets matching the screenshot:
        // Food: 2,000 EGP (spent 1,500, remaining 500)
        // Transportation: 1,000 EGP (spent 700, remaining 300)
        // Shopping: 1,500 EGP (spent 1,600, remaining -100)
        // Bills: 1,000 EGP (spent 800, remaining 200)
        // Entertainment: 1,500 EGP (spent 900, remaining 600)
        // Total Budget: 7,000 EGP, Spent: 5,500 EGP, Remaining: 1,500 EGP (78% usage)
        if (food != null)
            budgetRepo.CreateBudget(user.Id, new BudgetDto(food.Id, 2000m, "monthly", null, null));
        if (transport != null)
            budgetRepo.CreateBudget(user.Id, new BudgetDto(transport.Id, 1000m, "monthly", null, null));
        if (shopping != null)
            budgetRepo.CreateBudget(user.Id, new BudgetDto(shopping.Id, 1500m, "monthly", null, null));
        if (bills != null)
            budgetRepo.CreateBudget(user.Id, new BudgetDto(bills.Id, 1000m, "monthly", null, null));
        if (entertainment != null)
            budgetRepo.CreateBudget(user.Id, new BudgetDto(entertainment.Id, 1500m, "monthly", null, null));

        // Seed Savings Goals matching the screenshot:
        // 1. Buy a Laptop: Target 30,000 EGP, Saved 12,000 EGP, Remaining 18,000 EGP, Dec 31, 2026
        // 2. Vacation: Target 10,000 EGP, Saved 0 EGP, Remaining 10,000 EGP, Jun 30, 2026
        var laptopGoal = savingsRepo.CreateGoal(user.Id, new SavingsGoalDto(
            "Buy a Laptop",
            30000m,
            12000m,
            MonthDate(currentMonth.AddMonths(2), DateTime.DaysInMonth(currentMonth.AddMonths(2).Year, currentMonth.AddMonths(2).Month)),
            "MacBook Pro for development work"
        ));

        var vacationGoal = savingsRepo.CreateGoal(user.Id, new SavingsGoalDto(
            "Vacation",
            10000m,
            0m,
            MonthDate(currentMonth.AddMonths(8), DateTime.DaysInMonth(currentMonth.AddMonths(8).Year, currentMonth.AddMonths(8).Month)),
            "Summer trip to coastal resort"
        ));
    }
}
