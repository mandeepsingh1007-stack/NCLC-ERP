---
name: phase-implement
description: Execute one implementation phase from plan through verification. Mandatory workflow: read HLD/LLD, read phase-state.json, architect review, database review, implement, unit test, integration test, security review, UX review, code review, HLD compliance, phase gate. No phase may be marked complete before this workflow.
disable-model-invocation: true
---

Input: phase name or phase number.

## Mandatory Workflow

The following sequence is NON-NEGOTIABLE. No phase may be marked complete without all steps.

1. **READ HLD/LLD** — `docs/architecture/FINAL-MASTER-HLD-LLD-v2.md`
2. **READ PHASE STATE** — `docs/agent-state/phase-state.json` and `ACTIVE.md`
3. **ARCHITECT REVIEW** — invoke architect agent for design validation
4. **DATABASE REVIEW** — invoke database-engineer agent for schema/migration review (when applicable)
5. **IMPLEMENT** — implement in vertical slices
6. **UNIT TEST** — write and run unit tests immediately
7. **INTEGRATION TEST** — write and run integration tests (CI if Docker unavailable)
8. **SECURITY REVIEW** — invoke security-reviewer agent for security-sensitive changes
9. **UX REVIEW** — invoke ux-reviewer agent for user-facing changes
10. **CODE REVIEW** — invoke code-reviewer agent for final review
11. **HLD COMPLIANCE** — run `scripts/hld-compliance.ps1` or verify schema against HLD
12. **PHASE GATE** — run `scripts/phase-gate.ps1` — this is the ONLY authority on completion

## Failure → Remediation Loop

If ANY step fails:
- Investigate and fix the root cause
- Rerun the failed step
- Rerun ALL prior steps that could be affected
- Rerun phase-gate

Do NOT silently suppress failures.
Do NOT weaken tests to make implementation pass.
Do NOT delete security checks to unblock development.

## Architecture Decisions

If implementation encounters a decision not resolved by the HLD/LLD:
- STOP
- Create `docs/adr/XXXX-<decision>.md` with:
  - Context, Decision, Alternatives, Consequences
  - Security impact, Database impact, API impact
  - Frontend impact, Testing impact, Migration impact
- Do not proceed until ADR is recorded

## Completion Protocol

After phase-gate returns PASS:
1. Update `docs/agent-state/phase-state.json`:
   - status: "accepted"
   - gateStatus: "pass"
   - nextPhaseUnlocked: true
   - lastGateRun: current timestamp
2. Update `docs/agent-state/ACTIVE.md` with completion report
3. Create git tag `phase-N-accepted` (local only, do not push)
4. Report: changed files, tests run, gate result, known warnings, next phase

## Rules
- Never declare a phase complete based on a single agent's claim
- CI-PENDING is NOT PASS
- A phase with failed mandatory checks cannot unlock the next phase
- Never bypass hooks, gate scripts, or quality gates
- Never modify production systems unless explicitly authorized
- Never expose secrets
