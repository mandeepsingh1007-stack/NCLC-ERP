---
name: security-review
description: Perform a security review of platform code and metadata execution.
context: fork
agent: security-reviewer
---

Review the requested change for:
- authorization bypass
- tenant/org leakage
- SQL injection
- unsafe dynamic identifiers
- expression/ValRule code execution
- unsafe file handling
- secret exposure
- plugin/module loading risks
- audit gaps
- overbroad data projection
Return severity and remediation.
