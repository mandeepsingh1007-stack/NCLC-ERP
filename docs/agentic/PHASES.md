# Recommended Claude Code Implementation Phases

## Phase 0 — Repository bootstrap
Toolchain, solution structure, CI baseline, local PostgreSQL/Redis, testing harness, CLAUDE.md and agentic setup.

## Phase 1 — Dictionary foundation
SysElement, translations, SysReference, list/table/search references, SysValRule, SysTable/SysColumn constraints and seed data.

## Phase 2 — Metadata runtime
Metadata graph/cache, type/reference/ValRule validation, context variables, PO lifecycle, factory and generated X classes.

## Phase 3 — Generic data API
Generic CRUD, QueryBuilder, pagination/filter/sort, projection filtering, consistent error model, optimistic concurrency.

## Phase 4 — React runtime
Metadata contract, generic forms, grids, lookup/search, display logic, field groups, menus, loading/error/empty/accessibility states.

## Phase 5 — Security and tenancy
Identity/session, client/org, roles, window/process/table/column/record/private access, export permissions, defense-in-depth.

## Phase 6 — Processes and workflow
Process execution, scheduler, workflow definitions/runtime, activities, transitions, document engine boundary.

## Phase 7 — Platform services
Audit, attachments, sequences, trees, module/package tracking.

## Phase 8 — Module lifecycle
Manifest, dependencies, migration runner, dictionary ownership, assembly registration, cache invalidation, install/upgrade tests.

## Phase 9 — Production hardening
Observability, load tests, security tests, resilience, backup/restore, migration rehearsal, CI/CD, release gates.

## Phase 10 — First reference module
Build a complete module end-to-end. It must prove that standard CRUD requires no module-specific React CRUD screen.
