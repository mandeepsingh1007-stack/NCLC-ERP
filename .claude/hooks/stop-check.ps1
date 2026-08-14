# Phase Stop Gate Hook
# Runs when Claude attempts to stop after claiming a phase is complete.
# Prevents false completion by running the phase gate.

$projectRoot = $PSScriptRoot
cd $PSScriptRoot/../../

$phaseStateFile = "docs/agent-state/phase-state.json"
$activeStateFile = "docs/agent-state/ACTIVE.md"

if (-not (Test-Path $phaseStateFile)) {
    Write-Output "Stop gate: phase-state.json not found. Allowing stop."
    exit 0
}

try {
    $state = Get-Content $phaseStateFile -Raw | ConvertFrom-Json
    $currentPhase = $state.currentPhase
    $status = $state.status
    $gateStatus = $state.gateStatus
}
catch {
    Write-Output "Stop gate: Could not parse phase-state.json. Allowing stop."
    exit 0
}

# If phase is already accepted, allow stop freely
if ($status -eq "accepted") {
    Write-Output "Stop gate: Phase $currentPhase is already accepted. Allowing stop."
    exit 0
}

# If CI is pending, allow stop only if explicitly recorded
if ($gateStatus -eq "ci_pending") {
    Write-Output "Stop gate: Phase $currentPhase is CI_PENDING."
    Write-Output "Do NOT mark phase accepted. Do NOT unlock next phase."
    Write-Output "Phase 1 integration tests await CI verification."
    Write-Output "Stopping is allowed but phase is NOT accepted."
    exit 0
}

# Run the phase gate script
$gateScript = "scripts/phase-gate.ps1"
if (Test-Path $gateScript) {
    Write-Output "Stop gate: Running phase gate before allowing stop..."
    $gateResult = & $gateScript 2>&1
    $gateExit = $LASTEXITCODE

    Write-Host ""
    Write-Host "=== Phase Gate Result ==="
    Write-Host $gateResult

    if ($gateExit -eq 0) {
        Write-Output "Stop gate: Phase gate PASSED. Allowing stop."
        exit 0
    } elseif ($gateExit -eq 3) {
        Write-Output "Stop gate: Phase gate is CI_PENDING. Allowing stop but NOT marking phase accepted."
        exit 0
    } else {
        Write-Output ""
        Write-Output "STOP BLOCKED: Phase $currentPhase gate FAILED."
        Write-Output "The following checks did not pass:"
        Write-Output $gateResult | Select-String "FAIL|BLOCKED"
        Write-Output ""
        Write-Output "Do NOT claim phase completion. Fix the failing checks and try again."
        exit 1
    }
}
else {
    Write-Output "Stop gate: phase-gate.ps1 not found. Allowing stop."
    exit 0
}
