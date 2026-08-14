# Dangerous Command Guard Hook
# Checks before execution for high-risk operations.
# This hook is invoked via PreToolUse for Bash commands where supported.
# For versions that don't support PreToolUse, this file serves as documentation.

param(
    [string]$Command
)

$blocked = @()

# Database destructive operations
if ($Command -match '(?i)DROP\s+(DATABASE|SCHEMA|TABLE)') {
    $blocked += "BLOCKED: DROP DATABASE/SCHEMA/TABLE is dangerous. Use explicit authorization."
}

if ($Command -match '(?i)TRUNCATE\s+\w+') {
    $blocked += "BLOCKED: TRUNCATE is destructive. Use explicit authorization."
}

if ($Command -match '(?i)DELETE\s+FROM\s+.*WHERE\s+1') {
    $blocked += "BLOCKED: DELETE FROM with WHERE 1 deletes all rows. Dangerous."
}

# Git dangerous operations
if ($Command -match '(?i)git\s+push\s+--force') {
    $blocked += "BLOCKED: git push --force is dangerous and can corrupt shared branches."
}

if ($Command -match '(?i)git\s+reset\s+--hard') {
    $blocked += "BLOCKED: git reset --hard destroys uncommitted changes."
}

if ($Command -match '(?i)git\s+checkout\s+--') {
    $blocked += "BLOCKED: git checkout -- may discard changes."
}

# Secrets exposure
if ($Command -match '(?i)echo\s+.*(?i)(password|secret|token|key)\s*=\s*["\x27]') {
    $blocked += "BLOCKED: Command appears to expose secrets in output."
}

# Mass file deletion
if ($Command -match '(?i)rm\s+-rf\s+/') {
    $blocked += "BLOCKED: rm -rf / is universally destructive."
}

if ($Command -match '(?i)rm\s+-rf\s+.*\s+-\w*f') {
    $blocked += "BLOCKED: Forced mass file deletion detected."
}

if ($blocked.Count -gt 0) {
    foreach ($msg in $blocked) {
        Write-Host "WARNING: $msg"
    }
    Write-Host ""
    Write-Host "To proceed, you must explicitly confirm each blocked operation."
    exit 1
}

exit 0
