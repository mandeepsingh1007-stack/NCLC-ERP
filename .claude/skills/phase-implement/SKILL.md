---
name: phase-implement
description: Execute one implementation phase from plan through verification.
disable-model-invocation: true
---

Input: phase name or phase number.

Workflow:
1. Read HLD/LLD.
2. Read current ACTIVE.md.
3. Inspect repository and git status.
4. Identify exact acceptance criteria.
5. Ask architect/relevant reviewer to inspect design when needed.
6. Implement in vertical slices.
7. Write tests immediately.
8. Run format/lint/build/unit/integration/E2E as applicable.
9. Run security review for protected areas.
10. Run code review.
11. Update ACTIVE.md and phase artifacts.
12. Stop if an architecture decision is unresolved; create ADR instead of guessing.
