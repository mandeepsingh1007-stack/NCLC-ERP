$state = "docs/agent-state/ACTIVE.md"
if (Test-Path $state) {
  Write-Output "Stop check: ACTIVE.md exists. Before ending, ensure it records tests, blockers and next actions."
}
