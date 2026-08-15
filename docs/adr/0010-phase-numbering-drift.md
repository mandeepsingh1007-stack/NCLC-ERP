# ADR-010: Phase Numbering Drift from HLD/LLD

- **Status**: Accepted
- **Date**: 2026-08-16
- **Deciders**: Claude (orchestrator), reviewed by phase-gate agent

## Context

The HLD/LLD (FINAL-MASTER-HLD-LLD-v2.md, Section 34) defines an 8-phase migration plan (0–7).
During implementation, the actual work diverged from the HLD/LLD phase numbering:

| HLD/LLD Phase | HLD/LLD Name | Our Phase | Our Name |
|---|---|---|---|
| 0 | Engineering foundation | 0 | Engineering foundation |
| 1 | Dictionary foundation | 1 | Dictionary foundation |
| 2 | Runtime | 2 | Runtime |
| 3 | UI | 3 | Data API + Meta API (backend half of UI) |
| 4 | Security | 4 | React Runtime (frontend half of UI) |
| 5 | Process/workflow | 5 | Security and Tenancy |
| 6 | Platform services | 6 | — |
| 7 | Production hardening | 7 | — |

The drift occurred because:
1. HLD/LLD Phase 3 (UI) was split into two implementation phases: Phase 3 (backend APIs) and Phase 4 (React frontend + finalization).
2. HLD/LLD Phase 4 (Security) maps to our Phase 5.
3. HLD/LLD Phase 5 (Process/workflow) maps to our Phase 6.

## Decision

Use **implementation phase numbers** for active development tracking (the numbers in `phase-state.json`, `ACTIVE.md`, and `phase-gates/`).
Map them to HLD/LLD sections for compliance verification.

Implementation phase → HLD/LLD section mapping:

| Our Phase | Name | HLD/LLD Section |
|---|---|---|
| 0 | Engineering Foundation | Section 7–9 |
| 1 | Dictionary Foundation | Section 7 (SysTable, SysColumn, SysValRule, etc.) |
| 2 | Runtime | Section 10–12 (MetadataGraph, cache, ValRule, PO lifecycle) |
| 3 | Data/Meta/Lookup APIs | Section 26–29 (Generic CRUD, meta API, lookup API, filter DSL) |
| 4 | React Runtime | Section 30–33 (Dynamic components, display logic, menu, state) |
| 5 | Security and Tenancy | Section 15 (Security) + Section 14 (Multi-client/Org) |
| 6 | Process/Workflow | Section 16–18 (Process, workflow, document engine) |
| 7 | Platform Services | Section 20–25 (Sequences, trees, audit, attachments, modules) |
| 8 | Production Hardening | Section 34–35 (Migration, observability, load testing) |

## Consequences

- `phase-gates/phase-5.json` was originally "Process / Workflow" — it must be updated to "Security and Tenancy" to match the actual implementation scope.
- ADRs for security (RBAC, authorization, session management) are required before Phase 5 implementation.
- Phase 6 gate file should remain as "Process / Workflow".
- HLD/LLD remains the authoritative design; our phase numbering is an implementation detail.
