$login = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method POST -Body (@{ email = "ahmed@example.com"; password = "Password123!" } | ConvertTo-Json) -ContentType "application/json"
$h = @{ "Authorization" = "Bearer $($login.token)" }
$summary = Invoke-RestMethod -Uri "http://localhost:5000/api/budgets/summary" -Headers $h
Write-Host "Total Budget:" $summary.data.totalBudget
Write-Host "Total Spent:" $summary.data.totalSpent
Write-Host "Remaining:" $summary.data.remaining
Write-Host "Percentage:" $summary.data.percentage
