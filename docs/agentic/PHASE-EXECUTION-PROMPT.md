# Phase Execution Prompt

Use this prompt with Claude Code:

Execute `<PHASE>` from `docs/agentic/PHASES.md`.

Before coding:
1. Read `CLAUDE.md`.
2. Read the relevant HLD/LLD sections.
3. Read `docs/agent-state/ACTIVE.md`.
4. Inspect repository structure, existing code and tests.
5. Ask the architect agent to identify design risks if the phase crosses layers.

During coding:
- Work in vertical slices.
- Preserve the HLD/LLD as the architecture baseline.
- Do not guess unresolved ADR decisions.
- Add tests with each slice.
- Do not bypass security, tenancy, validation or audit paths.
- Use isolated worktrees for parallel implementation tasks.

Before declaring complete:
- format/lint
- build
- unit tests
- integration tests
- API contract tests
- E2E tests where applicable
- security review
- code review
- update `docs/agent-state/ACTIVE.md`

Return:
- implementation summary
- files changed
- tests and exact results
- architecture decisions
- known risks
- next phase
