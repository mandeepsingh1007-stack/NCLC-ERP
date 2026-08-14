# PreCompact Hook — Persist agent state before context compaction
$state = "docs/agent-state/ACTIVE.md"
$phaseState = "docs/agent-state/phase-state.json"

# Build comprehensive state snapshot
Write-Output "PreCompact: persisting agent state..."

$phase = "UNKNOWN"
$phaseStatus = "UNKNOWN"
$currentTask = ""
$gateStatus = "UNKNOWN"
$filesChanged = ""
$testsRun = ""
$failures = ""
$adrs = ""
$nextActions = ""

if (Test-Path $state) {
    $content = Get-Content $state -Raw
    # Extract key fields
    if ($content -match '## Current Phase\s*\n(.+?)(?=\n##)') { $phase = $Matches[1].Trim() }
    if ($content -match '## Phase Status\s*\n(.+?)(?=\n##)') { $phaseStatus = $Matches[1].Trim() }
    if ($content -match '## Current Task\s*\n(.+?)(?=\n##)') { $currentTask = $Matches[1].Trim() }
    if ($content -match '## Gate Status\s*\n(.+?)(?=\n##)') { $gateStatus = $Matches[1].Trim() }
    if ($content -match '## Files Changed\s*\n(.+?)(?=\n##)') { $filesChanged = $Matches[1].Trim() }
    if ($content -match '## Tests\s*\n(.+?)(?=\n##)') { $testsRun = $Matches[1].Trim() }
    if ($content -match '## Failed Checks\s*\n(.+?)(?=\n##)') { $failures = $Matches[1].Trim() }
    if ($content -match '## ADRs\s*\n(.+?)(?=\n##)') { $adrs = $Matches[1].Trim() }
    if ($content -match '## Next Actions\s*\n(.+?)(?=\n##)') { $nextActions = $Matches[1].Trim() }
}

if (Test-Path $phaseState) {
    $ps = Get-Content $phaseState -Raw | ConvertFrom-Json
    $phase = "Phase $($ps.currentPhase) - $($ps.phaseName)"
    $phaseStatus = $ps.status
    $gateStatus = $ps.gateStatus
}

# Write state to temp file for postcompact to reload
$compactStateFile = "$PSScriptRoot/.compact-state.txt"
@"
Phase: $phase
PhaseStatus: $phaseStatus
CurrentTask: $currentTask
GateStatus: $gateStatus
FilesChanged: $filesChanged
TestsRun: $testsRun
Failures: $failures
ADRs: $adrs
NextActions: $nextActions
Timestamp: $(Get-Date -Format "o")
"@ | Set-Content $compactStateFile -Encoding UTF8

Write-Output "PreCompact: state persisted. Before continuing after compaction, reload CLAUDE.md, phase-state.json, ACTIVE.md, and relevant phase gates."
