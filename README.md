# 🌿 Pennywise — Complete Full-Stack Personal Finance & Budget Management Application

Pennywise is a complete, real, and fully functional personal finance and budget management web application. It features a lightweight **C# .NET 10 backend**, persistent **SQLite database**, and a clean **framework-free frontend** (HTML5, CSS3, Vanilla JavaScript, and scalable vector graphics) with zero external runtime UI dependencies.

---

## 🎨 Official Brand Identity & Color Palette

Pennywise uses an exact, modern emerald and navy financial palette:

| Variable | Hex Value | Role |
| :--- | :--- | :--- |
| `--primary-green` | `#165E34` | Primary brand CTA and active accents |
| `--accent-blue` | `#7E88EF` | Soft blue highlights and links |
| `--secondary-text` | `#4A607C` | Secondary captions, subheadings, and icons |
| `--muted-accent` | `#B4BCC9` | Subtle borders and placeholder elements |
| `--card-background` | `#F5F5F7` | Elevated card surfaces |
| `--global-background` | `#E8F8F0` | Soft mint page backdrop |
| `--sidebar-bg` | `#0A101D` | Deep navy sidebar and landing surface |

Both **Light** and **Dark** themes are fully supported and can be switched dynamically in Settings, persisting to SQLite.

---

## 🚀 Application Structure & Pages

The application is structured into 11 interconnected pages:

1. **Landing Page (`/` or `index.html`)**: Dark navy presentation showcasing product features, hero dashboard illustration, how-it-works steps, and authentication shortcuts.
2. **Register (`/register` or `register.html`)**: Real database account registration with password hashing, terms agreement, and validation.
3. **Login (`/login` or `login.html`)**: Secure authentication verifying credentials against SQLite with session persistence.
4. **Dashboard (`/dashboard` or `dashboard.html`)**: Real-time financial summary (Total Income, Total Expenses, Available Balance, Savings Progress), pure SVG visualizations (Income vs. Expenses grouped bars, Expenses by Category donut chart, and Spending Trend line chart), recent transactions, and monthly budget overview.
5. **Transactions (`/transactions` or `transactions.html`)**: Comprehensive transaction ledger supporting search, type filtering, category filtering, monthly filtering, pagination, modal-based CRUD, and cash flow calculations.
6. **Budgets (`/budgets` or `budgets.html`)**: Monthly budget progress tracking, remaining spending allowances, and real-time overspending alerts.
7. **Categories (`/categories` or `categories.html`)**: Tabbed category management (Expense and Income) with custom icon and color pickers, preventing deletion of categories linked to transactions.
8. **Reports & Analytics (`/reports` or `reports.html`)**: Date range filtering, top spending category highlights, visual charts, and instant RFC 4180-compliant **CSV export**.
9. **Savings Goals (`/savings-goals` or `savings-goals.html`)**: Savings target tracking with deposit contributions and withdrawal support.
10. **Profile & Settings (`/settings` or `settings.html`)**: Profile updates, password changes, dynamic currency switching (EGP default, USD, EUR, GBP, SAR, AED), and theme toggling.
11. **Custom 404 (`/404` or `404.html`)**: Branded fallback route with custom SVG illustration and navigation recovery.

---

## 📂 Project Directory Structure

```text
Pennywise-Complete-Source/
├── Frontend/
│   ├── index.html
│   ├── login.html
│   ├── register.html
│   ├── dashboard.html
│   ├── transactions.html
│   ├── budgets.html
│   ├── categories.html
│   ├── reports.html
│   ├── savings-goals.html
│   ├── settings.html
│   ├── 404.html
│   ├── css/
│   │   ├── variables.css
│   │   ├── reset.css
│   │   ├── global.css
│   │   ├── auth.css
│   │   ├── landing.css
│   │   ├── dashboard.css
│   │   ├── transactions.css
│   │   ├── budgets.css
│   │   ├── categories.css
│   │   ├── reports.css
│   │   ├── savings-goals.css
│   │   ├── settings.css
│   │   └── responsive.css
│   ├── js/
│   │   ├── app.js
│   │   ├── api.js
│   │   ├── auth.js
│   │   ├── navigation.js
│   │   ├── utils.js
│   │   ├── dashboard.js
│   │   ├── transactions.js
│   │   ├── budgets.js
│   │   ├── categories.js
│   │   ├── reports.js
│   │   ├── savings-goals.js
│   │   └── settings.js
│   └── assets/
│       ├── images/
│       │   ├── wallet-illustration.svg
│       │   ├── hero-dashboard.svg
│       │   └── 404-illustration.svg
│       └── icons/
│           └── logo.svg
├── Backend/
│   ├── Pennywise.csproj
│   ├── Program.cs
│   ├── Configuration/
│   ├── Data/
│   │   ├── DatabaseContext.cs
│   │   └── DataSeeder.cs
│   ├── Models/
│   │   ├── User.cs
│   │   ├── Category.cs
│   │   ├── Transaction.cs
│   │   ├── Budget.cs
│   │   ├── SavingsGoal.cs
│   │   └── Dtos.cs
│   ├── Repositories/
│   │   ├── UserRepository.cs
│   │   ├── CategoryRepository.cs
│   │   ├── TransactionRepository.cs
│   │   ├── BudgetRepository.cs
│   │   └── SavingsGoalRepository.cs
│   ├── Services/
│   │   ├── PasswordHasher.cs
│   │   ├── AuthHelper.cs
│   │   └── AnalyticsService.cs
│   └── Routes/
│       └── ApiEndpoints.cs
├── README.md
└── .gitignore
```

---
## 👥 Team Members & Responsibilities

Pennywise is a collaborative full-stack web application developed by a team of six members. Each team member is responsible for a specific part of the application, contributing to the overall user experience and functionality.

### Team Structure

| # | Team Member | Assigned Responsibilities | Main Deliverables |
|---|---|---|---|
| 1 | **Ibrahim Elghazaly** | Login & Registration | Login page, registration page, form validation, authentication integration, and connection to the backend authentication API. |
| 2 | **Ghofran Mohamed** | Landing Page & Dashboard | Landing page design, application introduction, dashboard interface, financial summaries, charts, and overview of the user's financial activity. |
| 3 | **Mohamed Saeed** | Budgets & Savings Goals | Budget management, budget progress tracking, savings goals, savings contributions, and progress calculations. |
| 4 | **Ahmed Saeed Khalifa** | Transactions | Income and expense management, transaction creation, editing and deletion, transaction history, search, and filtering. |
| 5 | **Youssef Hegazy** | Categories & Profile Settings | Income and expense categories, category management, user profile, personal information, and application preferences. |
| 6 | **Ziad El-Feki** | Reports & Analytics | Financial reports, income and expense analysis, spending by category, monthly comparisons, charts, and report export functionality. |

### Individual Contributions

#### 1. Ibrahim Elghazaly — Authentication
Responsible for the user authentication interface and its integration with the backend.

Key responsibilities:
- Build the Login and Registration interfaces using HTML, CSS, and Vanilla JavaScript.
- Validate user input and display appropriate error messages.
- Integrate authentication forms with the C# backend.
- Handle successful login, registration, and navigation to the existing Dashboard.
- Ensure the authentication pages follow the Pennywise design system.

#### 2. Ghofran Mohamed — Landing Page & Dashboard
Responsible for the application's landing page and main dashboard experience.

Key responsibilities:
- Introduce Pennywise and its core features through the landing page.
- Design the main dashboard layout.
- Display financial summaries based on actual user data.
- Present recent transactions, budget summaries, and savings progress.
- Integrate dashboard charts with backend data.

#### 3. Mohamed Saeed — Budgets & Savings Goals
Responsible for helping users plan their spending and track their savings.

Key responsibilities:
- Implement budget management interfaces.
- Display budget limits, spending, and remaining amounts.
- Implement savings goal management.
- Track savings contributions and progress toward financial targets.
- Connect the relevant interfaces to persistent backend data.

#### 4. Ahmed Saeed Khalifa — Transactions
Responsible for managing the user's financial transactions.

Key responsibilities:
- Implement income and expense transaction management.
- Support adding, editing, viewing, and deleting transactions.
- Implement transaction search, filtering, and sorting.
- Display transaction details and history.
- Ensure transaction changes are reflected in relevant financial summaries.

#### 5. Youssef Hegazy — Categories & Profile Settings
Responsible for financial category management and user profile settings.

Key responsibilities:
- Implement income and expense category management.
- Support creating, editing, and deleting categories.
- Build profile and settings interfaces.
- Manage profile information and application preferences.
- Ensure category and profile changes are persisted through the backend.

#### 6. Ziad El-Feki — Reports & Analytics
Responsible for transforming financial records into meaningful reports.

Key responsibilities:
- Implement financial reports and analytics interfaces.
- Analyze income, expenses, and spending patterns.
- Display spending distribution by category.
- Provide monthly and date-range comparisons.
- Implement report export functionality.

### Collaboration & Integration

All team members collaborate to integrate their individual modules into one application.

The project follows a shared development approach:
- **Frontend:** HTML5, CSS3, and Vanilla JavaScript.
- **Backend:** C#.
- **Database:** SQLite, where configured in the project.
- **Version Control:** Git and GitHub.
- **Deployment:** Render, subject to the required hosting configuration and persistent storage setup.

Each module should communicate with the appropriate backend endpoints and use consistent design styles, data formats, and navigation patterns.

### Project Completion & Verification

Assigned responsibilities describe each member's area of ownership. Actual implementation and completion should be confirmed by reviewing the source code and testing the relevant functionality.

The project is considered fully integrated when all modules work together, persist data correctly, and pass the applicable build and functional checks.

## 🛠️ Prerequisites & Local Setup

- **.NET 10 SDK** installed on Windows, macOS, or Linux. The application targets `net10.0`; .NET 8/9 runtimes cannot run it.
- Any modern web browser (Edge, Chrome, Firefox, Safari).

### Run from Windows PowerShell or VS Code

```powershell
.\run.ps1
```

This invokes the existing ASP.NET Core project using its Development launch profile. The C# server hosts both the frontend and `/api`; no Node.js process or separate frontend build is needed. Open:

**http://localhost:5000**

To run without the helper script, use `dotnet run --project .\Backend\Pennywise.csproj`. Do not open `Frontend\index.html` directly with `file://`: that only displays a static page and cannot serve the API.

### Build and integration tests

```powershell
dotnet restore .\Backend\Pennywise.csproj
dotnet build .\Backend\Pennywise.csproj --configuration Release --no-restore
```

`.\test_suite.ps1` is an API integration suite for an already-running application. It creates test accounts and financial records, so run it against a disposable database, not your personal database. Set `PENNYWISE_TEST_BASE_URL` if the server is not at `http://localhost:5000`. GitHub Actions runs it against an isolated database automatically.

## Database and configuration

- By default SQLite is stored at `Backend\Data\pennywise.db`; it is retained across restarts and is not reset at startup.
- Set `PENNYWISE_DB_PATH` to choose another absolute or content-root-relative database path. The legacy `Database:Path` setting is also supported.
- Set `PORT` to change the HTTP port. Without it, launch profiles are respected and the normal local port is 5000.
- Session cookies are HttpOnly and secure by default in Production. Set `PENNYWISE_COOKIE_SECURE=false` only for explicit HTTP-only local testing; keep the default in production.
- The demo user and sample financial data are seeded only in the Development environment, and only when `ahmed@example.com` is absent. Production starts without demo credentials. The existing local database is not overwritten.

Development demo account:

- **Email**: `ahmed@example.com`
- **Password**: `Password123!`

Register a separate account through `/register.html` to test new-user onboarding. Each user's financial data, categories, budgets, and savings goals are scoped to that account.

## API overview

All endpoints are same-origin under `/api` and require the session created by registration/login unless noted:

- `POST /auth/register`, `POST /auth/login`, `POST /auth/logout`, `GET /auth/me`, `POST /auth/change-password`
- `GET /dashboard/summary`, `GET /dashboard/charts`, `GET /dashboard/recent-transactions`
- `/transactions` — list/filter, get by ID, create, update, delete
- `/categories` — list/filter, get by ID, create, update, delete
- `/budgets` and `/budgets/summary` — CRUD and computed period summaries
- `/savings-goals` — CRUD, contributions, withdrawals and transaction history
- `/reports/summary`, `/reports/expenses-by-category`, `/reports/monthly-comparison`, `/reports/top-category`, `/reports/export-csv`
- `GET /profile`, `PUT /profile`, `GET /preferences`, `PUT /preferences`
- `GET /healthz` — liveness/database check

## GitHub Actions and deployment

`.github/workflows/ci.yml` restores and builds with .NET 10, runs `test_suite.ps1` against isolated SQLite and PostgreSQL databases, and deploys to Render on pushes to `main` only after both test jobs pass.

The included `Dockerfile` publishes the ASP.NET Core backend and frontend together. For a persistent local Docker database, build and run with a named volume:

```powershell
docker build -t pennywise .
docker run --name pennywise -p 5000:5000 -v pennywise-data:/data pennywise
```

The container uses `PENNYWISE_DB_PATH=/data/pennywise.db`; keep `/data` on a persistent volume in any hosted environment. SQLite data on ephemeral container storage will be lost when the container is replaced.

## Free Render deployment with PostgreSQL

`render.yaml` defines a free Docker web service. Render's free service filesystem is ephemeral, so this deployment uses an external PostgreSQL database instead of writing financial data to the container. The application still defaults to the existing SQLite database for local use; it switches providers only when `PENNYWISE_DB_PROVIDER=postgres` is configured. The existing local SQLite file is left untouched, and the hosted PostgreSQL schema starts empty; this setup does not copy local financial data to a third party.

One possible free PostgreSQL provider is [Neon](https://neon.com/). Create a PostgreSQL project, then copy its **Npgsql/.NET connection string**. Neon's [current Free plan](https://neon.com/docs/introduction/plans) documents a 1 GB storage limit per project and compute scale-to-zero; quotas and plan terms can change. Render's [free web services](https://render.com/docs/free) can also sleep when idle, so the first request may take longer.

### One-time setup

1. Push the repository to GitHub on the `main` branch.
2. In Neon, open **Connect** and note the host, database, role, and password. In Render, create a new **Blueprint** from the repository and confirm the `pennywise` free web service. When prompted for `PENNYWISE_POSTGRES_CONNECTION_STRING`, enter an Npgsql ADO.NET connection string in this form (use your own values, not the URI copied from Neon):
   ```text
   Host=<neon-host>;Port=5432;Database=<database>;Username=<role>;Password=<password>;SSL Mode=VerifyFull;Channel Binding=Require
   ```
   Render stores this as a service secret; do not add it to source files.
3. In the Render service's **Settings**, create a deploy hook. In GitHub, add a repository Actions secret named `RENDER_DEPLOY_HOOK_URL` containing that hook URL.
4. Push a commit to `main`. GitHub Actions runs the SQLite and PostgreSQL integration suites first, then calls the Render deploy hook if its secret is configured. Before setup is complete, the deploy job emits a warning and skips deployment. Pull requests run both test jobs but do not deploy.

`PENNYWISE_DB_PROVIDER=postgres` is set by the Blueprint. If the connection-string secret is missing, the application exits with a configuration error rather than silently creating a temporary SQLite database. The PostgreSQL tables and indexes are created at startup. To keep local development on SQLite, leave `PENNYWISE_DB_PROVIDER` unset (or set it to `sqlite`); configure `PENNYWISE_DB_PATH` as before if needed.

If deploying manually instead of using the GitHub deploy hook, set the same environment variables in Render and leave `PENNYWISE_POSTGRES_CONNECTION_STRING` private. GitHub Pages alone cannot host the C# API.

## Project structure

- `Backend\` — ASP.NET Core Minimal API, SQLite schema, repositories and business logic.
- `Frontend\` — static HTML, CSS, vanilla JavaScript and local SVG assets.
- `.github\workflows\ci.yml` — build and integration-test automation.
- `render.yaml` — free Render Blueprint configuration using PostgreSQL.
- `run.ps1` — one-command local Windows startup.
- `Dockerfile` — combined backend/frontend production container.

---

## 📊 Authoritative Financial Calculation Rules

- **Total Income**: Sum of qualifying `income` transactions for the specified period.
- **Total Expenses**: Sum of qualifying `expense` transactions for the specified period.
- **Available Balance**: Calculated as `Total Income - Total Expenses` for the dashboard's current calendar month.
- **Budget Spent**: Sum of `expense` transactions matching the budget category in the current weekly, monthly, or yearly period. Income transactions never affect budget spending.
- **Budget Remaining**: `Budget Limit - Budget Spent`. Exceeded budgets trigger an alert status and negative remaining balance.
- **Savings Progress**: `Current Amount / Target Amount`, safely capped and formatted.

---

## 📦 Exporting & GitHub Readiness

To push this project to GitHub:

1. Initialize git repository:
   ```bash
   git init
   git add .
   git commit -m "Initial commit of Pennywise full-stack application"
   ```
2. The provided `.gitignore` and `.dockerignore` exclude compiled binaries (`bin/`, `obj/`), local SQLite files (`*.db`), and container build artifacts.
3. Push to your GitHub repository:
   ```bash
   git branch -M main
   git remote add origin https://github.com/<your-username>/Pennywise.git
   git push -u origin main
   ```

A source distribution ZIP `Pennywise-Complete-Source.zip` can be generated directly using PowerShell.
