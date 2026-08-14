$state = "docs/agent-state/ACTIVE.md"
if (-not (Test-Path $state)) {
  New-Item -ItemType Directory -Force -Path "docs/agent-state" | Out-Null
  @"
# Active Agent State

## Current phase
UNKNOWN

## Resume note
Update this file before continuing after compaction.
"@ | Set-Content $state
}
Write-Output "PreCompact: durable agent state file exists. Update it with the current task before compaction."
