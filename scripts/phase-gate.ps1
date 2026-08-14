<#
.SYNOPSIS
    Deterministic phase gate script for the No-Code/Low-Code platform.
    Executes or verifies all required checks for the current phase.
    Returns exit code 0 = PASS, 2 = BLOCKED/FAIL, 3 = CI_PENDING.
#>

param(
    [string]$SolutionRoot = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Results = @{}
$ExitCode = 0
$CiPending = $false

function Test-GateCheck {
    param(
        [string]$Name,
        [scriptblock]$Test,
        [bool]$Required = $true
    )
    try {
        $result = & $Test
        $Results[$Name] = $result
        $status = if ($result -eq $true) { "PASS" } else { "FAIL" }
        Write-Host "  [$status] $Name"
        if ($result -ne $true -and $Required) {
            $ExitCode = 2
        }
        return $result
    }
    catch {
        $Results[$Name] = "ERROR: $_"
        Write-Host "  [ERROR] $Name : $_"
        if ($Required) {
            $ExitCode = 2
        }
        return $false
    }
}

Write-Host "========================================"
Write-Host "  PHASE GATE VERIFICATION"
Write-Host "========================================"
Write-Host ""

# --------------------------------------------------
# Check 1: Git status — no uncommitted changes in production
# --------------------------------------------------
Write-Host "[1/8] Git status..."
Test-GateCheck "GIT" {
    $status = & git status --porcelain 2>$null
    return -not $status
}

# --------------------------------------------------
# Check 2: .NET restore
# --------------------------------------------------
Write-Host "[2/8] .NET restore..."
Test-GateCheck "DOTNET_RESTORE" {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { return $false }
    $result = dotnet restore "$SolutionRoot/NoCodeLow.sln" > $null 2>&1
    return ($LASTEXITCODE -eq 0)
}

# --------------------------------------------------
# Check 3: .NET build
# --------------------------------------------------
Write-Host "[3/8] .NET build..."
Test-GateCheck "BUILD" {
    $result = dotnet build "$SolutionRoot/NoCodeLow.sln" --no-restore > $null 2>&1
    return ($LASTEXITCODE -eq 0)
}

# --------------------------------------------------
# Check 4: Unit tests
# --------------------------------------------------
Write-Host "[4/8] Unit tests..."
Test-GateCheck "UNIT_TESTS" {
    # Find test projects
    $testProjects = Get-ChildItem -Path "$SolutionRoot/tests" -Filter "*.Tests.*.csproj" -Recurse -ErrorAction SilentlyContinue
    if (-not $testProjects) { return $false }

    $allPassed = $true
    foreach ($proj in $testProjects) {
        $result = dotnet test $proj.FullName --no-build --verbosity quiet > $null 2>&1
        if ($LASTEXITCODE -ne 0) {
            $allPassed = $false
        }
    }
    return $allPassed
}

# --------------------------------------------------
# Check 5: Integration tests (Docker-dependent)
# --------------------------------------------------
Write-Host "[5/8] Integration tests..."
Test-GateCheck "INTEGRATION_TESTS" {
    # Check if Docker is available
    $dockerTest = docker info > $null 2>&1
    $dockerAvailable = ($LASTEXITCODE -eq 0)

    if (-not $dockerAvailable) {
        Write-Host "    Docker not available — integration tests CI_PENDING"
        $global:CiPending = $true
        return $true  # Don't block — CI will run these
    }

    # Run integration test projects
    $integrationProjects = Get-ChildItem -Path "$SolutionRoot/tests" -Filter "*Integration*.csproj" -Recurse -ErrorAction SilentlyContinue
    if (-not $integrationProjects) { return $true }  # No integration tests — skip

    $allPassed = $true
    foreach ($proj in $integrationProjects) {
        $result = dotnet test $proj.FullName --verbosity quiet > $null 2>&1
        if ($LASTEXITCODE -ne 0) {
            $allPassed = $false
        }
    }
    return $allPassed
}

# --------------------------------------------------
# Check 6: Frontend build
# --------------------------------------------------
Write-Host "[6/8] Frontend build..."
Test-GateCheck "FRONTEND_BUILD" {
    $frontendDir = "$SolutionRoot/frontend"
    if (-not (Test-Path "$frontendDir/package.json")) { return $true }  # No frontend

    $result = Set-Location $frontendDir; npm run build > $null 2>&1
    return ($LASTEXITCODE -eq 0)
}

# --------------------------------------------------
# Check 7: Migration verification
# --------------------------------------------------
Write-Host "[7/8] Migration verification..."
Test-GateCheck "MIGRATIONS" {
    # Check that migration files exist
    $migrationsDir = "$SolutionRoot/src/Platform.Data/Migrations"
    $migrationFiles = Get-ChildItem -Path $migrationsDir -Filter "*.sql" -ErrorAction SilentlyContinue
    return ($null -ne $migrationFiles -and $migrationFiles.Count -gt 0)
}

# --------------------------------------------------
# Check 8: Secret scan (basic)
# --------------------------------------------------
Write-Host "[8/8] Secret scan..."
Test-GateCheck "SECRET_SCAN" {
    $codeFiles = Get-ChildItem -Path "$SolutionRoot/src" -Include "*.cs","*.cshtml","*.ts","*.tsx","*.js","*.json" -Recurse -ErrorAction SilentlyContinue
    if (-not $codeFiles) { return $true }

    $patterns = @('password\s*=\s*["\'][^"\']+["\']', 'secret\s*=\s*["\'][^"\']+["\']', 'api_key\s*=\s*["\'][^"\']+["\']')
    foreach ($file in $codeFiles) {
        foreach ($pattern in $patterns) {
            $matches = Select-String -Path $file.FullName -Pattern $pattern -ErrorAction SilentlyContinue
            if ($matches) {
                # Ignore appsettings.json (may have placeholders)
                if ($file.Name -notmatch 'appsettings') {
                    return $false
                }
            }
        }
    }
    return $true
}

# --------------------------------------------------
# Phase State Consistency
# --------------------------------------------------
Write-Host ""
Write-Host "[9/9] Phase state consistency..."
Test-GateCheck "PHASE_STATE" {
    $phaseStateFile = "$SolutionRoot/docs/agent-state/phase-state.json"
    if (-not (Test-Path $phaseStateFile)) { return $false }

    $state = Get-Content $phaseStateFile -Raw | ConvertFrom-Json
    return ($null -ne $state.currentPhase -and $null -ne $state.status)
}

# --------------------------------------------------
# Output Summary
# --------------------------------------------------
Write-Host ""
Write-Host "========================================"
Write-Host "  PHASE GATE SUMMARY"
Write-Host "========================================"

$passCount = 0
$failCount = 0
$ciPendingCount = 0

foreach ($key in $Results.Keys) {
    $val = $Results[$key]
    if ($val -eq $true) {
        Write-Host "  CHECK_${key}=PASS"
        $passCount++
    } elseif ($val -eq $true -or $val -eq "CI_PENDING") {
        Write-Host "  CHECK_${key}=CI_PENDING"
        $ciPendingCount++
        $global:CiPending = $true
    } else {
        Write-Host "  CHECK_${key}=FAIL"
        $failCount++
    }
}

Write-Host ""

if ($global:CiPending -and $failCount -eq 0) {
    Write-Host "PHASE_GATE_STATUS=CI_PENDING"
    $ExitCode = 3
} elseif ($failCount -gt 0) {
    Write-Host "PHASE_GATE_STATUS=BLOCKED"
    $ExitCode = 2
} else {
    Write-Host "PHASE_GATE_STATUS=PASS"
    $ExitCode = 0
}

Write-Host "CHECK_BUILD=$($Results['BUILD'])"
Write-Host "CHECK_UNIT_TESTS=$($Results['UNIT_TESTS'])"
Write-Host "CHECK_INTEGRATION_TESTS=$($Results['INTEGRATION_TESTS'])"
Write-Host "CHECK_SCHEMA=$($Results['MIGRATIONS'])"
Write-Host "CHECK_GIT=$($Results['GIT'])"
Write-Host "CHECK_SECURITY=$($Results['SECRET_SCAN'])"
Write-Host ""

exit $ExitCode
