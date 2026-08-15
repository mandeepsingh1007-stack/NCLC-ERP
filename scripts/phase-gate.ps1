<#
.SYNOPSIS
    Deterministic phase gate script for the No-Code/Low-Code platform.
    Executes or verifies all required checks for the current phase.
    Returns exit code 0 = PASS, 2 = BLOCKED/FAIL, 3 = CI_PENDING.
#>

param(
    [string]$SolutionRoot = ""
)

# Resolve SolutionRoot to repo root (parent of scripts/)
if ([string]::IsNullOrEmpty($SolutionRoot)) {
    $SolutionRoot = Join-Path $PSScriptRoot ".." | Resolve-Path
}

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Results = @{}
$global:CiPending = $false
$script:GateExitCode = 0

function Test-GateCheck {
    param(
        [string]$Name,
        [scriptblock]$Test,
        [bool]$Required = $true
    )
    try {
        $result = & $Test
        $Results[$Name] = $result
        $status = if ($result -eq $true) { "PASS" } elseif ($result -eq "CI_PENDING") { "CI_PENDING" } else { "FAIL" }
        Write-Host "  [$status] $Name"
        if ($result -ne $true -and $result -ne "CI_PENDING" -and $Required) {
            if ($script:GateExitCode -lt 2) { $script:GateExitCode = 2 }
        }
        return $result
    }
    catch {
        $Results[$Name] = "ERROR: $_"
        Write-Host "  [ERROR] $Name : $_"
        if ($Required) {
            if ($script:GateExitCode -lt 2) { $script:GateExitCode = 2 }
        }
        return $false
    }
}

Write-Host "========================================"
Write-Host "  PHASE GATE VERIFICATION"
Write-Host "========================================"
Write-Host ""

# Helper: run dotnet and return exit code reliably
function Invoke-Dotnet {
    param(
        [string]$DotnetArgs
    )
    $outputFile = Join-Path $env:TEMP "nclc-gate-$([guid]::NewGuid().ToString('N')).txt"
    $errFile = Join-Path $env:TEMP "nclc-gate-$([guid]::NewGuid().ToString('N'))-err.txt"
    $startArgs = @{
        FilePath = "dotnet"
        ArgumentList = $DotnetArgs
        NoNewWindow = $true
        Wait = $true
        PassThru = $true
        RedirectStandardOutput = $outputFile
        RedirectStandardError = $errFile
    }
    $process = Start-Process @startArgs
    $rc = $process.ExitCode

    if ($rc -eq 0) {
        $outputs = Get-Content $outputFile -ErrorAction SilentlyContinue
        Write-Host "    $outputs"
    } else {
        $errors = Get-Content $errFile -ErrorAction SilentlyContinue
        Write-Host "    $errors"
    }

    Remove-Item $outputFile, $errFile -ErrorAction SilentlyContinue
    return $rc
}

# --------------------------------------------------
# Check 1: Git status
# --------------------------------------------------
Write-Host "[1/8] Git status..."
Test-GateCheck "GIT" {
    $status = & git status --porcelain 2>&1
    return -not $status
}

# --------------------------------------------------
# Check 2: .NET restore
# --------------------------------------------------
Write-Host "[2/8] .NET restore..."
Test-GateCheck "DOTNET_RESTORE" {
    $slnPath = Join-Path $SolutionRoot "Platform.sln"
    $rc = Invoke-Dotnet -DotnetArgs "restore `"$slnPath`""
    return ($rc -eq 0)
}

# --------------------------------------------------
# Check 3: .NET build
# --------------------------------------------------
Write-Host "[3/8] .NET build..."
Test-GateCheck "BUILD" {
    $slnPath = Join-Path $SolutionRoot "Platform.sln"
    $rc = Invoke-Dotnet -DotnetArgs "build `"$slnPath`" --verbosity quiet"
    return ($rc -eq 0)
}

# --------------------------------------------------
# Check 4: Unit tests (exclude Integration and SchemaContract — they need DB)
# --------------------------------------------------
Write-Host "[4/8] Unit tests..."
Test-GateCheck "UNIT_TESTS" {
    $testProjects = Get-ChildItem -Path (Join-Path $SolutionRoot "tests") -Filter "*.Tests.Core*.csproj" -Recurse -ErrorAction SilentlyContinue
    if (-not $testProjects) { return $false }

    $allPassed = $true
    foreach ($proj in $testProjects) {
        $rc = Invoke-Dotnet -DotnetArgs "test `"$($proj.FullName)`" --verbosity quiet --no-build"
        if ($rc -ne 0) { $allPassed = $false }
    }
    return $allPassed
}

# --------------------------------------------------
# Check 5: Integration tests (Docker-dependent)
# --------------------------------------------------
Write-Host "[5/8] Integration tests..."
Test-GateCheck "INTEGRATION_TESTS" {
    try {
        $dockerOutputFile = Join-Path $env:TEMP "nclc-docker-test.txt"
        $dockerErrFile = Join-Path $env:TEMP "nclc-docker-test-err.txt"
        $startArgs = @{
            FilePath = "docker"
            ArgumentList = "info"
            NoNewWindow = $true
            Wait = $true
            PassThru = $true
            RedirectStandardOutput = $dockerOutputFile
            RedirectStandardError = $dockerErrFile
        }
        $process = Start-Process @startArgs
        $dockerAvailable = ($process.ExitCode -eq 0)
        Remove-Item $dockerOutputFile, $dockerErrFile -ErrorAction SilentlyContinue
    } catch {
        $dockerAvailable = $false
    }

    if (-not $dockerAvailable) {
        Write-Host "    Docker not available - integration tests CI_PENDING"
        $global:CiPending = $true
        return "CI_PENDING"
    }

    $integrationProjects = Get-ChildItem -Path (Join-Path $SolutionRoot "tests") -Filter "*Integration*.csproj" -Recurse -ErrorAction SilentlyContinue
    if (-not $integrationProjects) { return $true }

    $allPassed = $true
    foreach ($proj in $integrationProjects) {
        $rc = Invoke-Dotnet -DotnetArgs "test `"$($proj.FullName)`" --verbosity quiet --no-build"
        if ($rc -ne 0) { $allPassed = $false }
    }
    return $allPassed
}

# --------------------------------------------------
# Check 6: Frontend build
# --------------------------------------------------
Write-Host "[6/8] Frontend build..."
Test-GateCheck "FRONTEND_BUILD" {
    $frontendDir = Join-Path $SolutionRoot "frontend"
    if (-not (Test-Path (Join-Path $frontendDir "package.json"))) { return $true }

    $outputFile = Join-Path $env:TEMP "nclc-frontend-build.txt"
    $errFile = Join-Path $env:TEMP "nclc-frontend-build-err.txt"
    $cmdLine = "/c cd /d `"$frontendDir`" && npm run build"
    $startArgs = @{
        FilePath = "cmd"
        ArgumentList = $cmdLine
        NoNewWindow = $true
        Wait = $true
        PassThru = $true
        RedirectStandardOutput = $outputFile
        RedirectStandardError = $errFile
    }
    $process = Start-Process @startArgs
    Remove-Item $outputFile -ErrorAction SilentlyContinue
    return ($process.ExitCode -eq 0)
}

# --------------------------------------------------
# Check 7: Migration verification
# --------------------------------------------------
Write-Host "[7/8] Migration verification..."
Test-GateCheck "MIGRATIONS" {
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

    $q = [char]39
    $dq = [char]34
    $patterns = @(
        "password\s*=\s*[$dq$q][^$dq$q]+[$dq$q]",
        "secret\s*=\s*[$dq$q][^$dq$q]+[$dq$q]",
        "api_key\s*=\s*[$dq$q][^$dq$q]+[$dq$q]"
    )
    foreach ($file in $codeFiles) {
        foreach ($pattern in $patterns) {
            $matches = Select-String -Path $file.FullName -Pattern $pattern -ErrorAction SilentlyContinue
            if ($matches) {
                if ($file.Name -notmatch 'appsettings') {
                    return $false
                }
            }
        }
    }
    return $true
}

# --------------------------------------------------
# Check 9: Phase State Consistency
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
    } elseif ($val -eq "CI_PENDING") {
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
    $script:GateExitCode = 3
} elseif ($failCount -gt 0) {
    Write-Host "PHASE_GATE_STATUS=BLOCKED"
    $script:GateExitCode = 2
} else {
    Write-Host "PHASE_GATE_STATUS=PASS"
    $script:GateExitCode = 0
}

Write-Host "CHECK_BUILD=$($Results['BUILD'])"
Write-Host "CHECK_UNIT_TESTS=$($Results['UNIT_TESTS'])"
Write-Host "CHECK_INTEGRATION_TESTS=$($Results['INTEGRATION_TESTS'])"
Write-Host "CHECK_SCHEMA=$($Results['MIGRATIONS'])"
Write-Host "CHECK_GIT=$($Results['GIT'])"
Write-Host "CHECK_SECURITY=$($Results['SECRET_SCAN'])"
Write-Host ""

exit $script:GateExitCode
