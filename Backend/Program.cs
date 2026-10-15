using Microsoft.Extensions.FileProviders;
using Pennywise.Data;
using Pennywise.Repositories;
using Pennywise.Routes;
using Pennywise.Services;

var builder = WebApplication.CreateBuilder(args);

// Honor an explicitly configured port, otherwise respect launch profiles/ASPNETCORE_URLS.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
else if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000");
}

// SQLite remains the local default; hosted deployments can opt into PostgreSQL.
var databaseProvider = builder.Configuration["PENNYWISE_DB_PROVIDER"]
    ?? builder.Configuration["Database:Provider"]
    ?? "sqlite";
var postgresConnectionString = builder.Configuration["PENNYWISE_POSTGRES_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("Postgres");
var usesPostgres = databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase);
if (!usesPostgres && !databaseProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("PENNYWISE_DB_PROVIDER must be either 'sqlite' or 'postgres'.");
}
if (usesPostgres && string.IsNullOrWhiteSpace(postgresConnectionString))
{
    throw new InvalidOperationException(
        "PENNYWISE_POSTGRES_CONNECTION_STRING is required when PENNYWISE_DB_PROVIDER is 'postgres'.");
}

var configuredDbPath = builder.Configuration["PENNYWISE_DB_PATH"]
    ?? builder.Configuration["Database:Path"];
var dbPath = string.IsNullOrWhiteSpace(configuredDbPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "Data", "pennywise.db")
    : Path.GetFullPath(configuredDbPath, builder.Environment.ContentRootPath);
var dbContext = new DatabaseContext(dbPath, usesPostgres ? postgresConnectionString : null);
dbContext.Initialize();

builder.Services.AddSingleton(dbContext);
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<TransactionRepository>();
builder.Services.AddScoped<BudgetRepository>();
builder.Services.AddScoped<SavingsGoalRepository>();
builder.Services.AddScoped<AnalyticsService>();

var app = builder.Build();

// Demo credentials and sample financial data are development-only.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var userRepo = scope.ServiceProvider.GetRequiredService<UserRepository>();
    var catRepo = scope.ServiceProvider.GetRequiredService<CategoryRepository>();
    var txRepo = scope.ServiceProvider.GetRequiredService<TransactionRepository>();
    var budgetRepo = scope.ServiceProvider.GetRequiredService<BudgetRepository>();
    var savingsRepo = scope.ServiceProvider.GetRequiredService<SavingsGoalRepository>();
    DataSeeder.SeedDemoUserIfEmpty(userRepo, catRepo, txRepo, budgetRepo, savingsRepo);
}

// Locate Frontend directory
var frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Frontend"));
if (!Directory.Exists(frontendPath))
{
    frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "Frontend"));
}

if (Directory.Exists(frontendPath))
{
    var fileProvider = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        DefaultFileNames = new List<string> { "index.html" }
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = ""
    });
}

// Map REST API Endpoints
app.MapPennywiseApi();

app.MapGet("/healthz", (DatabaseContext database) =>
{
    using var connection = database.CreateConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT 1;";
    command.ExecuteScalar();
    return Results.Ok(new { status = "healthy" });
});

// Friendly Page Route mappings
var pageRoutes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "/", "index.html" },
    { "/index", "index.html" },
    { "/login", "login.html" },
    { "/register", "register.html" },
    { "/dashboard", "dashboard.html" },
    { "/transactions", "transactions.html" },
    { "/budgets", "budgets.html" },
    { "/categories", "categories.html" },
    { "/reports", "reports.html" },
    { "/savings-goals", "savings-goals.html" },
    { "/settings", "settings.html" },
    { "/404", "404.html" }
};

foreach (var kvp in pageRoutes)
{
    var filePath = Path.Combine(frontendPath, kvp.Value);
    app.MapGet(kvp.Key, () =>
    {
        if (File.Exists(filePath))
        {
            return Results.File(filePath, "text/html; charset=utf-8");
        }
        return Results.NotFound();
    });
}

// Fallback for non-matching routes
app.MapFallback((HttpContext ctx) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = 404;
        return ctx.Response.WriteAsJsonAsync(new { success = false, message = "API endpoint not found." });
    }

    var notFoundPage = Path.Combine(frontendPath, "404.html");
    if (File.Exists(notFoundPage))
    {
        ctx.Response.StatusCode = 404;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        return ctx.Response.SendFileAsync(notFoundPage);
    }

    ctx.Response.StatusCode = 404;
    return ctx.Response.WriteAsync("404 Not Found");
});

app.Run();
