$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "Backend\Pennywise.csproj"
dotnet run --project $projectPath
