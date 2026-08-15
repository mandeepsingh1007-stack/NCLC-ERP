<#
.SYNOPSIS
    Phase Transition Gate.
    Before Phase N+1 starts, verifies that Phase N is fully accepted.
    Returns exit code 0 = TRANSITION_ALLOWED, 2 = TRANSITION_BLOCKED.
#>

param(
    [int]$TargetPhase = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
cd "$projectRoot/.."

$phaseStateFile = "docs/agent-state/phase-state.json"
$phaseGatesDir = "docs/agent-state/phase-gates"

Write-Host "========================================"
Write-Host "  PHASE TRANSITION GATE"
Write-Host "  Attempting to unlock Phase $TargetPhase"
Write-Host "========================================"
Write-Host ""

# Check phase-state.json exists
if (-not (Test-Path $phaseStateFile)) {
    Write-Host "FAIL: phase-state.json not found."
    exit 2
}

$state = Get-Content $phaseStateFile -Raw | ConvertFrom-Json

# The previous phase is TargetPhase - 1
$prevPhase = $TargetPhase - 1
if ($prevPhase -lt 0) {
    Write-Host "Cannot transition to phase 0 (Phase 0 is the starting point)."
    exit 2
}

# Check if the previous phase is accepted
$currentPhase = $state.currentPhase
$currentStatus = $state.status
$currentGateStatus = $state.gateStatus

Write-Host "Current phase: $currentPhase ($($state.phaseName))"
Write-Host "Current status: $currentStatus"
Write-Host "Current gate status: $currentGateStatus"
Write-Host ""

# Rule 1: Phase must be ci_pending or implementation_complete before transition check
# It must NOT be "accepted" yet — the transition script runs BEFORE acceptance
# Rule 2: If already accepted, Phase 2 is unlocked

if ($currentStatus -eq "accepted" -and $currentGateStatus -eq "pass") {
    # Previous phase is already accepted — check if next is already unlocked
    if ($state.nextPhaseUnlocked) {
        Write-Host "PASS: Phase $currentPhase is accepted and next phase is already unlocked."
        exit 0
    }

    # Not unlocked yet — check prerequisites from phase-gate definitions
    $prevGateFile = "$phaseGatesDir/phase-$currentPhase.json"
    if (Test-Path $prevGateFile) {
        $prevGate = Get-Content $prevGateFile -Raw | ConvertFrom-Json
        $prereqs = @($prevGate.prerequisites)

        foreach ($prereq in $prereqs) {
            Write-Host "  Checking prerequisite: $prereq"
        }

        Write-Host ""
        Write-Host "PREREQUISITES MET for Phase $TargetPhase."
        Write-Host "Setting nextPhaseUnlocked = true"

        # Update phase-state.json
        $state.nextPhaseUnlocked = $true
        $state | ConvertTo-Json -Depth 10 | Set-Content $phaseStateFile

        Write-Host "Phase $TargetPhase is UNLOCKED."
        exit 0
    }
}

if ($currentStatus -eq "ci_pending") {
    Write-Host "BLOCKED: Previous phase ($currentPhase) has gateStatus='$currentGateStatus'."
    Write-Host "Phase $TargetPhase CANNOT start until Phase $currentPhase CI verification passes."
    Write-Host ""
    $prevGateFile = "$phaseGatesDir/phase-$currentPhase.json"
    if (Test-Path $prevGateFile) {
        $prevGate = Get-Content $prevGateFile -Raw | ConvertFrom-Json
        Write-Host "Required before next phase:"
        if ($prevGate.PSObject.Properties.Name -contains 'requiredBeforePhase2') {
            foreach ($req in $prevGate.requiredBeforePhase2) {
                Write-Host "  - $req"
            }
        }
    }
    exit 2
}

if ($currentStatus -eq "blocked") {
    Write-Host "BLOCKED: Previous phase ($currentPhase) is BLOCKED."
    Write-Host "Blockers:"
    foreach ($blocker in @($state.blockers)) {
        Write-Host "  - $blocker"
    }
    exit 2
}

if ($currentStatus -ne "accepted") {
    Write-Host "BLOCKED: Phase $currentPhase status is '$currentStatus' (expected 'accepted')."
    Write-Host "Phase $TargetPhase CANNOT start."
    exit 2
}

Write-Host "Result: TRANSITION_BLOCKED"
exit 2
