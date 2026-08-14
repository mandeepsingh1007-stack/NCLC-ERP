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

## Agent operating protocol
For every phase:
1. Read the relevant HLD/LLD sections.
2. Inspect the existing repository before editing.
3. Produce a short implementation plan.
4. Implement in small vertical slices.
5. Add/update tests with the code.
6. Run formatting, lint, build, unit/integration tests and relevant E2E tests.
7. Run security checks for security-sensitive changes.
8. Review the diff for architecture drift.
9. Update ADRs/docs only when the decision or contract changes.
10. Report: changed files, tests run, failures, known risks, next step.

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
- UX is usable and accessible for user-facing changes.

## Context discipline
Do not dump large files into the main context.
Prefer targeted search/read.
Delegate large investigations to subagents.
Use skills for repeatable procedures.
Keep `docs/agent-state/ACTIVE.md` current.
Before compacting, record current phase, task, decisions, files changed, tests, failures and next actions.
After compaction, reload `ACTIVE.md`, the phase plan and relevant ADRs before continuing.

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
