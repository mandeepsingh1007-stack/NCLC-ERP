$state = "docs/agent-state/ACTIVE.md"
if (Test-Path $state) {
  Write-Output "=== ACTIVE AGENT STATE ==="
  Get-Content $state -TotalCount 120
}
