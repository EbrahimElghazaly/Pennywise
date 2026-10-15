# Pennywise Automated Verification Test Suite
$baseUrl = if ($env:PENNYWISE_TEST_BASE_URL) { $env:PENNYWISE_TEST_BASE_URL } else { "http://localhost:5000" }
$errors = 0
$passed = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET",
        $Body = $null,
        $Headers = @{},
        [int]$ExpectedStatus = 200,
        [scriptblock]$Validator = $null
    )

    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            UseBasicParsing = $true
        }
        if ($Body) {
            $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
            $params["ContentType"] = "application/json"
        }

        $res = Invoke-WebRequest @params
        if ($res.StatusCode -eq $ExpectedStatus) {
            $valid = $true
            if ($Validator) {
                $valid = & $Validator $res
            }
            if ($valid) {
                Write-Host " [PASS] $Name" -ForegroundColor Green
                $script:passed++
                return $res
            } else {
                Write-Host " [FAIL] $Name (Custom validation failed)" -ForegroundColor Red
                $script:errors++
                return $null
            }
        } else {
            Write-Host " [FAIL] $Name (Status $($res.StatusCode), expected $ExpectedStatus)" -ForegroundColor Red
            $script:errors++
            return $null
        }
    } catch {
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode.value__ -eq $ExpectedStatus) {
            Write-Host " [PASS] $Name (Expected status $ExpectedStatus)" -ForegroundColor Green
            $script:passed++
            return $_.Exception.Response
        } else {
            Write-Host " [FAIL] $Name - Exception: $($_.Exception.Message)" -ForegroundColor Red
            $script:errors++
            return $null
        }
    }
}

Write-Host "`n=== 1. VERIFYING HTML PAGES & STATIC ASSETS ===" -ForegroundColor Cyan
$health = Invoke-RestMethod -Uri "$baseUrl/healthz"
if ($health.status -eq "healthy") {
    Write-Host " [PASS] Health endpoint and database connection" -ForegroundColor Green
    $script:passed++
} else {
    Write-Host " [FAIL] Health endpoint returned an unexpected status" -ForegroundColor Red
    $script:errors++
}

$pages = @(
    @{ Name = "Page 1: Landing Page (/)"; Url = "$baseUrl/" },
    @{ Name = "Page 2: Register (/register.html)"; Url = "$baseUrl/register.html" },
    @{ Name = "Page 3: Login (/login.html)"; Url = "$baseUrl/login.html" },
    @{ Name = "Page 4: Dashboard (/dashboard.html)"; Url = "$baseUrl/dashboard.html" },
    @{ Name = "Page 5: Transactions (/transactions.html)"; Url = "$baseUrl/transactions.html" },
    @{ Name = "Page 6: Budgets (/budgets.html)"; Url = "$baseUrl/budgets.html" },
    @{ Name = "Page 7: Categories (/categories.html)"; Url = "$baseUrl/categories.html" },
    @{ Name = "Page 8: Reports (/reports.html)"; Url = "$baseUrl/reports.html" },
    @{ Name = "Page 9: Savings Goals (/savings-goals.html)"; Url = "$baseUrl/savings-goals.html" },
    @{ Name = "Page 10: Settings (/settings.html)"; Url = "$baseUrl/settings.html" },
    @{ Name = "Page 11: 404 Page (/404.html)"; Url = "$baseUrl/404.html" },
    @{ Name = "Fallback 404 Route (/nonexistent-route)"; Url = "$baseUrl/nonexistent-route"; Status = 404 },
    @{ Name = "Asset: Wallet Illustration SVG"; Url = "$baseUrl/assets/images/wallet-illustration.svg" },
    @{ Name = "Asset: Hero Dashboard SVG"; Url = "$baseUrl/assets/images/hero-dashboard.svg" },
    @{ Name = "Asset: Logo SVG"; Url = "$baseUrl/assets/icons/logo.svg" }
)

foreach ($p in $pages) {
    $status = if ($p.ContainsKey("Status")) { $p.Status } else { 200 }
    Test-Endpoint -Name $p.Name -Url $p.Url -ExpectedStatus $status
}

Write-Host "`n=== 2. VERIFYING AUTHENTICATION ===" -ForegroundColor Cyan
# Invalid Login
Test-Endpoint -Name "Reject invalid login credentials" -Url "$baseUrl/api/auth/login" -Method "POST" `
    -Body @{ email = "ahmed@example.com"; password = "WrongPassword123!" } -ExpectedStatus 400

# Valid Login
$loginRes = Test-Endpoint -Name "Login with demo account (ahmed@example.com)" -Url "$baseUrl/api/auth/login" -Method "POST" `
    -Body @{ email = "ahmed@example.com"; password = "Password123!"; rememberMe = $true } -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.success -eq $true -and $data.token -and $data.user.email -eq "ahmed@example.com")
    }

$loginData = $loginRes.Content | ConvertFrom-Json
$token = $loginData.token
$authHeader = @{ "Authorization" = "Bearer $token" }

# Current user session
Test-Endpoint -Name "Get current user profile (/api/auth/me)" -Url "$baseUrl/api/auth/me" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.user.fullName -eq "Ahmed Mohamed")
    }

# Register a brand new unique user
$uniqueEmail = "tester_$(Get-Random)@pennywise.test"
$regRes = Test-Endpoint -Name "Register brand new account ($uniqueEmail)" -Url "$baseUrl/api/auth/register" -Method "POST" `
    -Body @{
        fullName = "Test User"
        email = $uniqueEmail
        password = "Password123!"
        confirmPassword = "Password123!"
        agreeToTerms = $true
    } -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.success -eq $true -and $data.token)
    }
$registeredUser = $regRes.Content | ConvertFrom-Json
$registeredHeaders = @{ "Authorization" = "Bearer $($registeredUser.token)" }

Test-Endpoint -Name "Login with newly registered account" -Url "$baseUrl/api/auth/login" -Method "POST" `
    -Body @{ email = $uniqueEmail; password = "Password123!"; rememberMe = $false } -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.success -eq $true -and $data.user.email -eq $uniqueEmail)
    }

Test-Endpoint -Name "Update registered user's profile" -Url "$baseUrl/api/profile" -Method "PUT" -Headers $registeredHeaders `
    -Body @{ fullName = "Updated Test User"; email = $uniqueEmail; phoneNumber = "+1 555 0100" } -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.data.fullName -eq "Updated Test User" -and $data.data.phoneNumber -eq "+1 555 0100")
    }

Test-Endpoint -Name "Change registered user's password" -Url "$baseUrl/api/auth/change-password" -Method "POST" -Headers $registeredHeaders `
    -Body @{ currentPassword = "Password123!"; newPassword = "NewPassword123!"; confirmNewPassword = "NewPassword123!" } -ExpectedStatus 200
Test-Endpoint -Name "Login with changed password" -Url "$baseUrl/api/auth/login" -Method "POST" `
    -Body @{ email = $uniqueEmail; password = "NewPassword123!"; rememberMe = $false } -ExpectedStatus 200

# Reject duplicate email registration
Test-Endpoint -Name "Reject duplicate registration with same email" -Url "$baseUrl/api/auth/register" -Method "POST" `
    -Body @{
        fullName = "Duplicate User"
        email = $uniqueEmail
        password = "Password123!"
        confirmPassword = "Password123!"
        agreeToTerms = $true
    } -ExpectedStatus 400

Write-Host "`n=== 3. VERIFYING DASHBOARD & REPORTS METRICS ===" -ForegroundColor Cyan
Test-Endpoint -Name "Dashboard summary (/api/dashboard/summary)" -Url "$baseUrl/api/dashboard/summary" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.success -eq $true -and $data.data.totalIncome -gt 0 -and $data.data.availableBalance -gt 0)
    }

Test-Endpoint -Name "Dashboard charts (/api/dashboard/charts)" -Url "$baseUrl/api/dashboard/charts" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.data.monthlyComparison.Count -gt 0 -and $data.data.expensesByCategory.Count -gt 0)
    }

Test-Endpoint -Name "Dashboard recent transactions" -Url "$baseUrl/api/dashboard/recent-transactions" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.data.Count -gt 0)
    }

Write-Host "`n=== 4. VERIFYING TRANSACTIONS CRUD ===" -ForegroundColor Cyan
# Get Categories to link
$catRes = Invoke-RestMethod -Uri "$baseUrl/api/categories" -Headers $authHeader
$foodCat = $catRes.data | Where-Object { $_.name -eq "Food" } | Select-Object -First 1

# Category CRUD
$categoryName = "Automation Category $(Get-Random)"
$categoryRes = Test-Endpoint -Name "Create expense category" -Url "$baseUrl/api/categories" -Method "POST" -Headers $authHeader `
    -Body @{ name = $categoryName; type = "expense"; icon = "tag"; color = "#165E34" } -ExpectedStatus 200
$categoryId = ($categoryRes.Content | ConvertFrom-Json).data.id
Test-Endpoint -Name "Update expense category" -Url "$baseUrl/api/categories/$categoryId" -Method "PUT" -Headers $authHeader `
    -Body @{ name = "$categoryName Updated"; type = "expense"; icon = "tag"; color = "#165E34" } -ExpectedStatus 200
Test-Endpoint -Name "Reject transaction whose type conflicts with its category" -Url "$baseUrl/api/transactions" -Method "POST" -Headers $authHeader `
    -Body @{
        categoryId = $foodCat.id
        title = "Invalid category type"
        amount = 1
        type = "income"
        date = (Get-Date).ToString("yyyy-MM-dd")
        notes = ""
    } -ExpectedStatus 400

# Create Transaction
$createTxRes = Test-Endpoint -Name "Create new transaction" -Url "$baseUrl/api/transactions" -Method "POST" -Headers $authHeader `
    -Body @{
        categoryId = $foodCat.id
        title = "Automated Test Grocery"
        amount = 150.50
        type = "expense"
        date = (Get-Date).ToString("yyyy-MM-dd")
        notes = "Created during automated verification"
    } -ExpectedStatus 200

$createdTx = ($createTxRes.Content | ConvertFrom-Json).data
$txId = $createdTx.id

# Update Transaction
Test-Endpoint -Name "Update transaction ($txId)" -Url "$baseUrl/api/transactions/$txId" -Method "PUT" -Headers $authHeader `
    -Body @{
        categoryId = $foodCat.id
        title = "Updated Automated Grocery"
        amount = 175.00
        type = "expense"
        date = (Get-Date).ToString("yyyy-MM-dd")
        notes = "Updated notes"
    } -ExpectedStatus 200

# Search & Filter Transactions
Test-Endpoint -Name "Filter transactions by search query" -Url "$baseUrl/api/transactions?search=Updated" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.data.Count -gt 0)
    }

# Delete Transaction
Test-Endpoint -Name "Delete transaction ($txId)" -Url "$baseUrl/api/transactions/$txId" -Method "DELETE" -Headers $authHeader -ExpectedStatus 200

Write-Host "`n=== 5. VERIFYING BUDGETS & SAVINGS GOALS ===" -ForegroundColor Cyan
# Weekly budget period must use the current week, not the calendar month.
$weeklyExpense = Test-Endpoint -Name "Create transaction for weekly budget verification" -Url "$baseUrl/api/transactions" -Method "POST" -Headers $authHeader `
    -Body @{
        categoryId = $categoryId
        title = "Weekly budget verification"
        amount = 23.45
        type = "expense"
        date = (Get-Date).ToString("yyyy-MM-dd")
        notes = "Temporary test transaction"
    } -ExpectedStatus 200
$weeklyExpenseId = ($weeklyExpense.Content | ConvertFrom-Json).data.id
$weeklyBudget = Test-Endpoint -Name "Create weekly budget" -Url "$baseUrl/api/budgets" -Method "POST" -Headers $authHeader `
    -Body @{ categoryId = $categoryId; limitAmount = 100; period = "weekly" } -ExpectedStatus 200
$weeklyBudgetId = ($weeklyBudget.Content | ConvertFrom-Json).data.id
$yearlyBudget = Test-Endpoint -Name "Create yearly budget" -Url "$baseUrl/api/budgets" -Method "POST" -Headers $authHeader `
    -Body @{ categoryId = $categoryId; limitAmount = 1000; period = "yearly" } -ExpectedStatus 200
$yearlyBudgetId = ($yearlyBudget.Content | ConvertFrom-Json).data.id
Test-Endpoint -Name "Weekly budget includes current-week expense" -Url "$baseUrl/api/budgets/$weeklyBudgetId" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.data.period -eq "weekly" -and $data.data.spentAmount -eq 23.45)
    }
Test-Endpoint -Name "Yearly budget includes current-year expense" -Url "$baseUrl/api/budgets/$yearlyBudgetId" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.data.period -eq "yearly" -and $data.data.spentAmount -eq 23.45)
    }
Test-Endpoint -Name "Update weekly budget" -Url "$baseUrl/api/budgets/$weeklyBudgetId" -Method "PUT" -Headers $authHeader `
    -Body @{ categoryId = $categoryId; limitAmount = 125; period = "weekly" } -ExpectedStatus 200
Test-Endpoint -Name "Reject category type change while financial records use it" -Url "$baseUrl/api/categories/$categoryId" -Method "PUT" -Headers $authHeader `
    -Body @{ name = "$categoryName Updated"; type = "income"; icon = "tag"; color = "#165E34" } -ExpectedStatus 400
Test-Endpoint -Name "Reject budget assigned to an income category" -Url "$baseUrl/api/budgets" -Method "POST" -Headers $authHeader `
    -Body @{ categoryId = ($catRes.data | Where-Object { $_.type -eq "income" } | Select-Object -First 1).id; limitAmount = 100; period = "monthly" } -ExpectedStatus 400
Test-Endpoint -Name "Delete weekly test budget" -Url "$baseUrl/api/budgets/$weeklyBudgetId" -Method "DELETE" -Headers $authHeader -ExpectedStatus 200
Test-Endpoint -Name "Delete yearly test budget" -Url "$baseUrl/api/budgets/$yearlyBudgetId" -Method "DELETE" -Headers $authHeader -ExpectedStatus 200
Test-Endpoint -Name "Delete weekly test transaction" -Url "$baseUrl/api/transactions/$weeklyExpenseId" -Method "DELETE" -Headers $authHeader -ExpectedStatus 200
Test-Endpoint -Name "Delete test category" -Url "$baseUrl/api/categories/$categoryId" -Method "DELETE" -Headers $authHeader -ExpectedStatus 200

# Budgets summary
Test-Endpoint -Name "Budgets summary (/api/budgets/summary)" -Url "$baseUrl/api/budgets/summary" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.data.totalBudget -eq 7000 -and $data.data.totalSpent -eq 5500)
    }

# Savings goals summary & actions
$goalsRes = Invoke-RestMethod -Uri "$baseUrl/api/savings-goals" -Headers $authHeader
$laptopGoal = $goalsRes.data | Where-Object { $_.name -like "*Laptop*" } | Select-Object -First 1
$newGoalRes = Test-Endpoint -Name "Create savings goal" -Url "$baseUrl/api/savings-goals" -Method "POST" -Headers $authHeader `
    -Body @{ name = "Automation Goal"; targetAmount = 1000; currentAmount = 100; targetDate = (Get-Date).AddMonths(3).ToString("yyyy-MM-dd"); description = "Temporary test goal" } -ExpectedStatus 200
$newGoalId = ($newGoalRes.Content | ConvertFrom-Json).data.id
Test-Endpoint -Name "Update savings goal" -Url "$baseUrl/api/savings-goals/$newGoalId" -Method "PUT" -Headers $authHeader `
    -Body @{ name = "Updated Automation Goal"; targetAmount = 1200; currentAmount = 100; targetDate = (Get-Date).AddMonths(4).ToString("yyyy-MM-dd"); description = "Updated test goal" } -ExpectedStatus 200
Test-Endpoint -Name "Reject withdrawal above saved balance" -Url "$baseUrl/api/savings-goals/$newGoalId/withdrawals" -Method "POST" -Headers $authHeader `
    -Body @{ amount = 200; date = (Get-Date).ToString("yyyy-MM-dd"); notes = "Should fail" } -ExpectedStatus 400
Test-Endpoint -Name "Delete savings goal" -Url "$baseUrl/api/savings-goals/$newGoalId" -Method "DELETE" -Headers $authHeader -ExpectedStatus 200

# Contribution
Test-Endpoint -Name "Add contribution to savings goal" -Url "$baseUrl/api/savings-goals/$($laptopGoal.id)/contributions" -Method "POST" -Headers $authHeader `
    -Body @{
        amount = 500
        date = (Get-Date).ToString("yyyy-MM-dd")
        notes = "Bonus savings contribution"
    } -ExpectedStatus 200

# Withdrawal
Test-Endpoint -Name "Withdraw from savings goal" -Url "$baseUrl/api/savings-goals/$($laptopGoal.id)/withdrawals" -Method "POST" -Headers $authHeader `
    -Body @{
        amount = 500
        date = (Get-Date).ToString("yyyy-MM-dd")
        notes = "Returned bonus withdrawal"
    } -ExpectedStatus 200

Write-Host "`n=== 6. VERIFYING REPORTS & CSV EXPORT ===" -ForegroundColor Cyan
Test-Endpoint -Name "Reports summary" -Url "$baseUrl/api/reports/summary" -Headers $authHeader -ExpectedStatus 200
Test-Endpoint -Name "Top category" -Url "$baseUrl/api/reports/top-category" -Headers $authHeader -ExpectedStatus 200
$csvTx = Test-Endpoint -Name "Create transaction with CSV-escaped fields" -Url "$baseUrl/api/transactions" -Method "POST" -Headers $authHeader `
    -Body @{
        categoryId = $foodCat.id
        title = 'CSV, "quoted" title'
        amount = 1.23
        type = "expense"
        date = (Get-Date).ToString("yyyy-MM-dd")
        notes = 'note, "quoted" value'
    } -ExpectedStatus 200
$csvTxId = ($csvTx.Content | ConvertFrom-Json).data.id
Test-Endpoint -Name "CSV export escapes comma and quote characters" -Url "$baseUrl/api/reports/export-csv?startDate=$((Get-Date).ToString('yyyy-MM-dd'))&endDate=$((Get-Date).ToString('yyyy-MM-dd'))" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        return ($r.Content -match '"CSV, ""quoted"" title"' -and $r.Content -match '"note, ""quoted"" value"')
    }
Test-Endpoint -Name "Delete CSV verification transaction" -Url "$baseUrl/api/transactions/$csvTxId" -Method "DELETE" -Headers $authHeader -ExpectedStatus 200
Test-Endpoint -Name "Export valid CSV (/api/reports/export-csv)" -Url "$baseUrl/api/reports/export-csv" -Headers $authHeader -ExpectedStatus 200 `
    -Validator {
        param($r)
        return ($r.Content -like "*Date,Title,Category,Type,Amount,Notes*")
    }

Write-Host "`n=== 7. VERIFYING PROFILE & PREFERENCES ===" -ForegroundColor Cyan
Test-Endpoint -Name "Get Profile (/api/profile)" -Url "$baseUrl/api/profile" -Headers $authHeader -ExpectedStatus 200
Test-Endpoint -Name "Update Preferences to Dark theme and USD" -Url "$baseUrl/api/preferences" -Method "PUT" -Headers $authHeader `
    -Body @{ currency = "USD"; theme = "dark" } -ExpectedStatus 200 `
    -Validator {
        param($r)
        $data = $r.Content | ConvertFrom-Json
        return ($data.data.currency -eq "USD" -and $data.data.theme -eq "dark")
    }

# Reset Preferences back to EGP and Light
Test-Endpoint -Name "Reset Preferences to EGP and Light" -Url "$baseUrl/api/preferences" -Method "PUT" -Headers $authHeader `
    -Body @{ currency = "EGP"; theme = "light" } -ExpectedStatus 200
Test-Endpoint -Name "Reject unsupported preference currency" -Url "$baseUrl/api/preferences" -Method "PUT" -Headers $authHeader `
    -Body @{ currency = "XYZ"; theme = "light" } -ExpectedStatus 400
Test-Endpoint -Name "Reject malformed report date" -Url "$baseUrl/api/reports/summary?startDate=not-a-date" -Headers $authHeader -ExpectedStatus 400

Write-Host "`n=== 8. VERIFYING LOGOUT ===" -ForegroundColor Cyan
Test-Endpoint -Name "Logout session (/api/auth/logout)" -Url "$baseUrl/api/auth/logout" -Method "POST" -Headers $authHeader -ExpectedStatus 200

# Verify token is now invalid
Test-Endpoint -Name "Verify logged out token is rejected" -Url "$baseUrl/api/auth/me" -Headers $authHeader -ExpectedStatus 401

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "TEST SUITE COMPLETE: $passed PASSED, $errors FAILED" -ForegroundColor $(if ($errors -eq 0) { "Green" } else { "Red" })
Write-Host "==========================================`n" -ForegroundColor Cyan

if ($errors -gt 0) { exit 1 } else { exit 0 }
