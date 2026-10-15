$zipPath = "c:\Users\admin\Downloads\Pennywise-Complete-Source\Pennywise-Complete-Source.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

$tempDir = Join-Path $env:TEMP "Pennywise_Package"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Copy Frontend
Copy-Item -Path "Frontend" -Destination (Join-Path $tempDir "Frontend") -Recurse

# Copy Backend excluding bin, obj, and database files
$backendDest = Join-Path $tempDir "Backend"
New-Item -ItemType Directory -Path $backendDest | Out-Null

Get-ChildItem -Path "Backend" | Where-Object { $_.Name -notin @("bin", "obj") } | ForEach-Object {
    if ($_.PSIsContainer) {
        if ($_.Name -eq "Data") {
            $dataDest = Join-Path $backendDest "Data"
            New-Item -ItemType Directory -Path $dataDest | Out-Null
            Get-ChildItem -Path $_.FullName | Where-Object { $_.Extension -ne ".db" -and -not ($_.Name -like "*.db*") } | ForEach-Object {
                Copy-Item -Path $_.FullName -Destination $dataDest -Recurse
            }
        } else {
            Copy-Item -Path $_.FullName -Destination $backendDest -Recurse
        }
    } else {
        Copy-Item -Path $_.FullName -Destination $backendDest
    }
}

# Copy root documentation
Copy-Item -Path "README.md" -Destination (Join-Path $tempDir "README.md")
Copy-Item -Path ".gitignore" -Destination (Join-Path $tempDir ".gitignore")
Copy-Item -Path "test_suite.ps1" -Destination (Join-Path $tempDir "test_suite.ps1")

# Create ZIP archive
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

# Clean up temporary staging directory
Remove-Item -Path $tempDir -Recurse -Force

if (Test-Path $zipPath) {
    Write-Host "SUCCESS: ZIP archive created successfully at $zipPath"
    Get-Item $zipPath | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
} else {
    Write-Host "ERROR: ZIP archive was not created." -ForegroundColor Red
}
