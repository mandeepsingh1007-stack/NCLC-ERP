# No-Code / Low-Code Platform — Agentic Development Contract

## Authority
The authoritative product architecture is:
- `docs/architecture/FINAL-MASTER-HLD-LLD-v2.md`

Do not silently change architecture. Any deviation requires an ADR in `docs/adr/`.

## Mission
Build the metadata-driven No-Code / Low-Code platform described by the HLD/LLD using:
- Backend: .NET 8 / ASP.NET Core
- DB: PostgreSQL 15+
- Data: Dapper + Npgsql
- Frontend: React + TypeScript
- Forms: React Hook Form
- Data fetching: TanStack Query
- Local metadata cache: IMemoryCache
- Distributed cache: Redis
- Background jobs: Hangfire or Quartz.NET — decide by ADR before production
- Messaging: RabbitMQ/Kafka only when justified by ADR
- Authentication: Identity/JWT or external IdP — decide by ADR
- Migration: DbUp or FluentMigrator — decide by ADR

## Non-negotiable architecture rules
1. Metadata first. Generic behavior belongs in runtime metadata services.
2. PostgreSQL is metadata source of truth.
3. Generated `X_<Table>` code is disposable and never hand-edited.
4. Business rules belong in `M_<Table>`, validators, callouts, or domain services.
5. UI is never the security boundary.
6. All dynamic identifiers must be resolved from trusted metadata.
7. All values must be parameterized.
8. Tenant and organization predicates are applied centrally.
9. Workflow state and document state are separate.
10. Module-owned metadata must be upgrade-safe and must not overwrite user-owned metadata.
11. Cache invalidation occurs only after a successful metadata transaction.
12. Standard CRUD must not require module-specific React screens.
13. Every phase must finish with automated verification.
14. Never weaken tests to make implementation pass.
15. Never delete or bypass security checks to unblock development.

## PHASE GATE RULES (NON-NEGOTIABLE)
1. Never declare a phase complete based solely on implementation.
2. Every phase requires a phase gate (`scripts/phase-gate.ps1`).
3. Required tests may not be deleted or weakened.
4. CI-pending is not PASS. A phase with failed mandatory checks cannot unlock the next phase.
5. HLD/LLD is authoritative. Architecture ambiguity requires ADR.
6. Security-sensitive changes require security review.
7. Database changes require database review.
8. UI changes require UX/accessibility review.
9. Phase transitions are gated (`scripts/phase-transition.ps1`).
10. Never bypass hooks or gate scripts to claim completion.
11. Never modify production systems unless explicitly authorized.
12. Never expose secrets.
13. Generated X_<Table> code is disposable and must never contain business logic.
14. Business logic belongs in M_<Table>, validators, callouts, or domain services.
15. UI is never the security boundary.
16. Tenant/org security must be enforced server-side.
17. Dynamic SQL identifiers must come only from trusted metadata.
18. Values must always be parameterized.
19. Phase transitions are gated: previous phase must be accepted before next starts.
20. Generated code is disposable; hand-edited generated files indicate a bug.

## Agent operating protocol
For every phase:
1. Read the relevant HLD/LLD sections.
2. Inspect the existing repository before editing.
3. Read `docs/agent-state/phase-state.json` and `ACTIVE.md`.
4. Produce a short implementation plan.
5. Implement in small vertical slices.
6. Add/update tests with the code.
7. Run formatting, lint, build, unit/integration tests and relevant E2E tests.
8. Run security checks for security-sensitive changes.
9. Run UX review for user-facing changes.
10. Review the diff for architecture drift.
11. Update ADRs/docs only when the decision or contract changes.
12. Run `scripts/phase-gate.ps1` — this is the ONLY authority on phase completion.
13. Report: changed files, tests run, gate result, failures, known risks, next step.

## Definition of Done
A task is NOT done when code compiles. It is done only when:
- behavior is implemented,
- authorization is verified server-side,
- validation is covered,
- failure paths are covered,
- telemetry/logging is appropriate,
- tests pass,
- no unsafe dynamic SQL is introduced,
- API/metadata contracts are version-compatible,
- migrations are safe and repeatable,
- UX is usable and accessible for user-facing changes,
- phase gate passes (`scripts/phase-gate.ps1`).

## Context discipline
Do not dump large files into the main context.
Prefer targeted search/read.
Delegate large investigations to subagents.
Use skills for repeatable procedures.
Keep `docs/agent-state/ACTIVE.md` current.
Before compacting, record current phase, task, decisions, files changed, tests, failures and next actions.
After compaction, reload `ACTIVE.md`, `phase-state.json`, the phase plan and relevant ADRs before continuing.

## Protected areas
Treat these as architecture-sensitive:
- authentication/authorization
- tenant/org predicates
- QueryBuilder
- metadata cache invalidation
- migrations
- module loader
- expression/ValRule evaluators
- dynamic SQL construction
- generated-code pipeline
- workflow/document state transitions

Changes here require the appropriate reviewer subagent before completion.

## Phase Gate Architecture
```
                    HUMAN
                      v
               CLAUDE ORCHESTRATOR
                      |
        +-------------+-------------+
        |             |             |
        v             v             v
    ARCHITECT     DEVELOPERS       QA
        |             |             |
        +-------------+-------------+
                      |
                      v
              SECURITY REVIEW
                      |
                      v
               CODE REVIEW
                      |
                      v
             PHASE GATE AGENT
                      |
                      v
          DETERMINISTIC GATE SCRIPTS
                      |
              +-------+-------+
              |               |
             PASS            FAIL
              |               |
              v               v
        PHASE ACCEPTED      FIX/RETRY
              |
              v
        CI VERIFICATION
              |
              v
        PHASE UNLOCKED
              |
              v
          NEXT PHASE
```

## Phase State Machine
- `implementation_complete` — code written, tests pass locally
- `ci_pending` — awaiting CI verification; NOT accepted; next phase LOCKED
- `accepted` — all gates pass including CI; next phase UNLOCKED
- `blocked` — blockers present; must be resolved before proceeding
