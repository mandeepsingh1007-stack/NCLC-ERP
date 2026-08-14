# PostCompact Hook — Reload critical state after context compaction
$compactStateFile = "$PSScriptRoot/.compact-state.txt"

Write-Output "PostCompact: reloading critical state..."

if (Test-Path $compactStateFile) {
    $state = Get-Content $compactStateFile -Raw
    Write-Output "Previous session state:"
    Write-Output $state
}

Write-Output ""
Write-Output "RELOAD THESE FILES BEFORE CONTINUING:"
Write-Output "  1. CLAUDE.md (governing contract)"
Write-Output "  2. docs/agent-state/phase-state.json (phase state)"
Write-Output "  3. docs/agent-state/ACTIVE.md (active state tracking)"
Write-Output "  4. docs/agent-state/phase-gates/ (phase-specific gates)"
Write-Output "  5. docs/agent-state/phase-gates/phase-$(Get-Content $PSScriptRoot/.compact-state.txt -TotalCount1 | Select-String 'Phase:' | ForEach-Object { $_.Line -replace 'Phase: ', '' -replace ' - .*', '' }).json (current phase gate)"
Write-Output ""
Write-Output "Never rely on conversational memory for phase state."
