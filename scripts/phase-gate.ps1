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
Write-Host "[1/12] Git status..."
Test-GateCheck "GIT" {
    $status = & git status --porcelain 2>&1
    return -not $status
}

# --------------------------------------------------
# Check 2: .NET restore
# --------------------------------------------------
Write-Host "[2/12] .NET restore..."
Test-GateCheck "DOTNET_RESTORE" {
    $slnPath = Join-Path $SolutionRoot "Platform.sln"
    $rc = Invoke-Dotnet -DotnetArgs "restore `"$slnPath`""
    return ($rc -eq 0)
}

# --------------------------------------------------
# Check 3: .NET build
# --------------------------------------------------
Write-Host "[3/12] .NET build..."
Test-GateCheck "BUILD" {
    $slnPath = Join-Path $SolutionRoot "Platform.sln"
    $rc = Invoke-Dotnet -DotnetArgs "build `"$slnPath`" --verbosity quiet"
    return ($rc -eq 0)
}

# --------------------------------------------------
# Check 4: Unit tests (exclude Integration and SchemaContract — they need DB)
# --------------------------------------------------
Write-Host "[4/12] Unit tests..."
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
Write-Host "[5/12] Integration tests..."
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
Write-Host "[6/12] Frontend build..."
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
# Check 7: Frontend tests
# --------------------------------------------------
Write-Host "[7/12] Frontend tests..."
Test-GateCheck "FRONTEND_TESTS" {
    $frontendDir = Join-Path $SolutionRoot "frontend"
    if (-not (Test-Path (Join-Path $frontendDir "package.json"))) { return $true }

    $outputFile = Join-Path $env:TEMP "nclc-frontend-tests.txt"
    $errFile = Join-Path $env:TEMP "nclc-frontend-tests-err.txt"
    $cmdLine = "/c cd /d `"$frontendDir`" && npm test -- --watchAll=false"
    $startArgs = @{
        FilePath = "cmd"
        ArgumentList = $cmdLine
        NoNewWindow = $true
        Wait = $true
        PassThru = $true
        RedirectStandardOutput = $outputFile
        RedirectStandardError = $errFile
    }
    try {
        $process = Start-Process @startArgs
        Remove-Item $outputFile -ErrorAction SilentlyContinue
        return ($process.ExitCode -eq 0)
    }
    catch {
        Write-Host "    Frontend tests not available — CI_PENDING"
        $global:CiPending = $true
        return "CI_PENDING"
    }
}

# --------------------------------------------------
# Check 8: Bundle size
# --------------------------------------------------
Write-Host "[8/12] Bundle size..."
Test-GateCheck "BUNDLE_SIZE" {
    $frontendDir = Join-Path $SolutionRoot "frontend"
    $buildDir = Join-Path $frontendDir "dist"
    if (-not (Test-Path $buildDir)) { return $true }

    # Find the largest .js file in dist/ (likely the main bundle)
    $mainBundle = Get-ChildItem -Path $buildDir -Recurse -Filter "*.js" |
        Where-Object { $_.DirectoryName -eq $buildDir } |
        Sort-Object Length -Descending |
        Select-Object -First 1

    if (-not $mainBundle) {
        # Try assets subdirectory
        $assetsDir = Join-Path $buildDir "assets"
        if (Test-Path $assetsDir) {
            $mainBundle = Get-ChildItem -Path $assetsDir -Filter "*.js" |
                Sort-Object Length -Descending |
                Select-Object -First 1
        }
    }

    if (-not $mainBundle) {
        Write-Host "    No JS bundle found — skipping bundle size check"
        return $true
    }

    $rawSize = $mainBundle.Length
    # Gzip approximation: JS files typically compress to ~30-40% of original
    $estimatedGzip = [math]::Round($rawSize * 0.35)
    $maxBytes = 250 * 1024  # 250 KB

    if ($estimatedGzip -gt $maxBytes) {
        Write-Host "    Estimated gzipped size: $([math]::Round($estimatedGzip/1024, 1)) KB (threshold: 250 KB)"
        return $false
    }
    Write-Host "    Estimated gzipped size: $([math]::Round($estimatedGzip/1024, 1)) KB (threshold: 250 KB) — OK"
    return $true
}

# --------------------------------------------------
# Check 9: Migration verification
# --------------------------------------------------
Write-Host "[9/12] Migration verification..."
Test-GateCheck "MIGRATIONS" {
    $migrationsDir = "$SolutionRoot/src/Platform.Data/Migrations"
    $migrationFiles = Get-ChildItem -Path $migrationsDir -Filter "*.sql" -ErrorAction SilentlyContinue
    if (-not $migrationFiles -or $migrationFiles.Count -eq 0) { return $false }

    # Verify files are numbered sequentially
    $expectedCount = $migrationFiles.Count
    $allPresent = $true
    for ($i = 1; $i -le $expectedCount; $i++) {
        $expectedPrefix = "[$i]$(('0'*($i - 1)).Substring(-max 0, 3 - ($i.ToString().Length)))_"
        $found = $migrationFiles | Where-Object { $_.Name -match "^$i_" }
        if (-not $found) {
            Write-Host "    Missing migration: $i_"
            $allPresent = $false
        }
    }

    # Verify each file contains valid SQL keywords
    foreach ($f in $migrationFiles) {
        $content = Get-Content $f.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) {
            Write-Host "    Empty migration file: $($f.Name)"
            return $false
        }
        $hasSql = ($content -match '(?i)(CREATE|INSERT|ALTER|DROP|UPDATE|DELETE|SELECT|BEGIN|COMMIT)')
        if (-not $hasSql) {
            Write-Host "    No SQL statements found in: $($f.Name)"
            return $false
        }
    }

    return $allPresent
}

# --------------------------------------------------
# Check 10: Secret scan (basic)
# --------------------------------------------------
Write-Host "[10/12] Secret scan..."
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
                    Write-Host "    Potential secret in: $($file.Name)"
                    return $false
                }
            }
        }
    }
    return $true
}

# --------------------------------------------------
# Check 11: Phase state consistency
# --------------------------------------------------
Write-Host "[11/12] Phase state consistency..."
Test-GateCheck "PHASE_STATE" {
    $phaseStateFile = "$SolutionRoot/docs/agent-state/phase-state.json"
    if (-not (Test-Path $phaseStateFile)) { return $false }

    $state = Get-Content $phaseStateFile -Raw | ConvertFrom-Json
    $hasPhase = $null -ne $state.currentPhase
    $hasStatus = $null -ne $state.status
    $hasGateStatus = $null -ne $state.gateStatus
    $hasUnlocked = $null -ne $state.nextPhaseUnlocked

    return ($hasPhase -and $hasStatus -and $hasGateStatus -and $hasUnlocked)
}

# --------------------------------------------------
# Check 12: Phase name alignment with PHASES.md
# --------------------------------------------------
Write-Host "[12/12] Phase name alignment..."
Test-GateCheck "PHASE_NAME_ALIGNMENT" {
    $phasesMd = "$SolutionRoot/docs/agentic/PHASES.md"
    if (-not (Test-Path $phasesMd)) { return $true }

    $phasesContent = Get-Content $phasesMd -Raw -ErrorAction SilentlyContinue
    if (-not $phasesContent) { return $true }

    # Extract phase names from PHASES.md
    $phaseDir = "$SolutionRoot/docs/agent-state/phase-gates"
    $gateFiles = Get-ChildItem -Path $phaseDir -Filter "phase-*.json" -ErrorAction SilentlyContinue
    if (-not $gateFiles) { return $true }

    $mismatch = $false
    foreach ($gf in $gateFiles) {
        $matchNum = ($gf.BaseName -replace 'phase-', '')
        if (-not ([int]::TryParse($matchNum, [ref]$null))) { continue }

        $gateData = Get-Content $gf.FullName -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json
        if (-not $gateData) { continue }

        $gatePhase = $gateData.phase
        $gateName = $gateData.phaseName

        # Extract expected name from PHASES.md
        $expectedPattern = "^## Phase $gatePhase — (.+)$"
        $match = $phasesContent | Select-String -Pattern $expectedPattern -AllMatches
        if ($match.Matches.Count -eq 0) {
            Write-Host "    No PHASES.md entry for Phase $gatePhase"
            continue
        }
        $expectedName = $match.Matches[0].Groups[1].Value.Trim()

        if ($gateName -ne $expectedName) {
            Write-Host "    MISMATCH: phase-$gatePhase.json says '$gateName', PHASES.md says '$expectedName'"
            $mismatch = $true
        }
    }

    return -not $mismatch
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
Write-Host "CHECK_FRONTEND_TESTS=$($Results['FRONTEND_TESTS'])"
Write-Host "CHECK_BUNDLE_SIZE=$($Results['BUNDLE_SIZE'])"
Write-Host "CHECK_PHASE_NAME_ALIGNMENT=$($Results['PHASE_NAME_ALIGNMENT'])"
Write-Host ""

exit $script:GateExitCode
