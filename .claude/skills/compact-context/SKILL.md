---
name: compact-context
description: Safely prepare durable state before context compaction or session handoff.
disable-model-invocation: true
---

Before compacting:
- summarize current phase
- exact task
- completed work
- files changed
- tests run and results
- unresolved failures
- decisions
- assumptions
- next 3-10 actions
- relevant ADRs
- commands needed to resume

Write this to `docs/agent-state/ACTIVE.md`.
Never put large logs or code dumps in ACTIVE.md.
