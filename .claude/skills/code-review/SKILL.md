---
name: code-review
description: Perform a strict final code review against architecture, security and tests.
context: fork
agent: code-reviewer
---

Review the current diff and surrounding code.
Find correctness, security, architecture, performance and maintainability issues.
Do not rubber-stamp.
Return findings ordered P0-P3 with evidence and recommended fix.
