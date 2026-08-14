# Agentic Development Loop

## Main orchestrator
The main Claude Code session owns:
- phase sequencing
- architecture decisions
- integration
- acceptance
- final review

## Parallel workers
Use isolated worktrees for independent implementation tasks:
- backend
- frontend
- database
- tests
- security review
- UX review

Never have two agents edit the same architectural hotspot concurrently.

## Per-feature loop
Plan → Implement → Test → Security Review → UX Review (if UI) → Code Review → Integrate → Regression Test.

## Escalation
If an agent finds an HLD/LLD ambiguity:
STOP implementation, create ADR proposal, resolve it, then continue.

## Context management
- Keep main context small.
- Delegate repository-wide exploration.
- Use skills on demand.
- Maintain ACTIVE.md.
- Compact at natural phase boundaries, not in the middle of a transactional change.
