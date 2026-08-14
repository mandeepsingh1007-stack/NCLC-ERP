# Keep this hook fast. Project-specific formatters should be wired here only after toolchain discovery.
Write-Output "Post-edit: formatting/linting is intentionally delegated to the project toolchain after repository discovery."
