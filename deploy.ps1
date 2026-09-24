<#
    FaqCms — one-shot deploy script
    Run this on the new PC after copying the FaqCms folder over, BEFORE
    the first "dotnet run". It will:
      1. Find (or ask for) a SQL Server instance
      2. Create the FaqCms database + schema + seed data (via create_database.sql)
      3. Rewrite appsettings.json to point at that instance
      4. Restore NuGet packages so the app is ready to run

    Usage examples:
      .\deploy.ps1                                   # auto-detect a local instance
      .\deploy.ps1 -ServerInstance ".\SQLEXPRESS"    # target a specific instance
      .\deploy.ps1 -ServerInstance "SQLSERVER01" -SqlUser sa -SqlPassword "..."   # SQL auth instead of Windows auth
#>

param(
    [string]$ServerInstance,
    [string]$SqlUser,
    [string]$SqlPassword
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$sqlStep1 = Join-Path $root "sql\01_create_database.sql"
$sqlStep2 = Join-Path $root "sql\02_create_schema.sql"
$appsettings = Join-Path $root "appsettings.json"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    $msg" -ForegroundColor Green }
function Write-Warn2($msg){ Write-Host "    $msg" -ForegroundColor Yellow }

foreach ($f in @($sqlStep1, $sqlStep2)) {
    if (-not (Test-Path $f)) { throw "$f not found next to deploy.ps1." }
}

# ---------- 1. Find sqlcmd ----------
Write-Step "Checking for sqlcmd"
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    Write-Warn2 "sqlcmd was not found on PATH."
    Write-Warn2 "Install 'sqlcmd' (part of the free 'Microsoft Command Line Utilities for SQL Server' / SSMS install), then re-run this script."
    Write-Warn2 "Alternative: open create_database.sql in SQL Server Management Studio on the target server and run it manually, then edit appsettings.json's ConnectionStrings:Default yourself and skip this script."
    exit 1
}
Write-Ok "Found sqlcmd at $($sqlcmd.Source)"

# ---------- 2. Figure out which SQL Server instance to use ----------
Write-Step "Locating a SQL Server instance"
if (-not $ServerInstance) {
    $candidates = @(
        "$env:COMPUTERNAME\SQLEXPRESS",
        ".\SQLEXPRESS",
        "(localdb)\MSSQLLocalDB",
        "localhost"
    )
    foreach ($c in $candidates) {
        Write-Host "    Trying $c ..."
        $testArgs = @("-S", $c, "-Q", "SELECT 1")
        if ($SqlUser) { $testArgs += @("-U", $SqlUser, "-P", $SqlPassword) } else { $testArgs += "-E" }
        & $sqlcmd.Source @testArgs *> $null
        if ($LASTEXITCODE -eq 0) {
            $ServerInstance = $c
            break
        }
    }
    if (-not $ServerInstance) {
        Write-Warn2 "Couldn't auto-detect a reachable SQL Server instance."
        $ServerInstance = Read-Host "    Enter the SQL Server instance name (e.g. .\SQLEXPRESS, SERVERNAME\INSTANCE, or (localdb)\MSSQLLocalDB)"
    }
}
Write-Ok "Using instance: $ServerInstance"

# ---------- 3. Run the schema/seed scripts (two separate sqlcmd calls,
#              on purpose, so step 2 always runs against an existing,
#              freshly-created FaqCms database rather than in the same
#              batch/session as the CREATE DATABASE itself) ----------
Write-Step "Step 1/2: creating the FaqCms database on $ServerInstance"
$authArgs = @()
if ($SqlUser) { $authArgs += @("-U", $SqlUser, "-P", $SqlPassword) } else { $authArgs += "-E" }

& $sqlcmd.Source -S $ServerInstance @authArgs -b -i $sqlStep1
if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd failed while running 01_create_database.sql (exit code $LASTEXITCODE). See the output above for the SQL error."
}
Write-Ok "Database created."

Write-Step "Step 2/2: creating schema + seed data"
& $sqlcmd.Source -S $ServerInstance -d FaqCms @authArgs -b -i $sqlStep2
if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd failed while running 02_create_schema.sql (exit code $LASTEXITCODE). See the output above for the SQL error."
}
Write-Ok "Schema and seed data ready."

# ---------- 4. Rewrite the connection string in appsettings.json ----------
Write-Step "Updating appsettings.json"
$escapedServer = $ServerInstance -replace '\\', '\\\\'
if ($SqlUser) {
    $newConnStr = "Server=$escapedServer;Database=FaqCms;User Id=$SqlUser;Password=$SqlPassword;MultipleActiveResultSets=true;Encrypt=True;TrustServerCertificate=True"
} else {
    $newConnStr = "Server=$escapedServer;Database=FaqCms;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;TrustServerCertificate=True"
}

$json = Get-Content $appsettings -Raw | ConvertFrom-Json
$json.ConnectionStrings.Default = $newConnStr
$json | ConvertTo-Json -Depth 10 | Set-Content $appsettings -Encoding UTF8
Write-Ok "ConnectionStrings:Default now points at $ServerInstance"

# ---------- 5. Restore packages so it's ready to run ----------
Write-Step "Restoring NuGet packages"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    & $dotnet.Source restore (Join-Path $root "FaqCms.csproj")
    Write-Ok "Packages restored."
} else {
    Write-Warn2 ".NET SDK not found on PATH — install the .NET 8 SDK, then run 'dotnet restore' and 'dotnet run' from this folder."
}

Write-Step "Done"
Write-Ok "Next: cd into this folder and run 'dotnet run' (or publish/deploy to IIS as usual)."
Write-Ok "First login: whatever AdminAuth:Username / AdminAuth:Password are in appsettings.json (defaults to admin / changeme) — change the password from /admin/users after logging in."
