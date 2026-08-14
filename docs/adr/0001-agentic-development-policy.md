# ADR-0001 Agentic Development Policy

## Status
Accepted

## Decision
Claude Code is used as the primary implementation orchestrator. Work is divided into small phases and specialized subagents.

## Rules
- Main agent owns sequencing and final integration.
- Read-only research/review agents do not modify the main worktree.
- Implementation agents may use isolated worktrees for parallel work.
- Security-sensitive changes require security review.
- Database migrations require database review.
- UI changes require UX/accessibility review.
- No architecture deviation without an ADR.
