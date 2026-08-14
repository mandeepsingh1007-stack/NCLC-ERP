# Agentic Setup for the No-Code / Low-Code Platform

This package is a Claude Code development-control layer for the supplied FINAL MASTER HLD + LLD v2.

## Install
1. Copy this directory's contents into the root of your application repository.
2. Replace `docs/architecture/FINAL-MASTER-HLD-LLD-v2.md` with the supplied authoritative HLD/LLD.
3. Review `.claude/settings.json` for your OS/toolchain.
4. Copy `.mcp.json.example` to `.mcp.json` only after filling real MCP endpoints/commands.
5. Start Claude Code in the repository.
6. Run `/agents` and verify the custom agents.
7. Run `/hooks` and verify hooks.
8. Start with Phase 0 from `docs/agentic/PHASES.md`.

## Important
This package intentionally does not contain secrets or pretend MCP endpoints.
Connect only the services your organization actually uses.

## Recommended working pattern
Main agent:
- architecture
- orchestration
- integration
- final acceptance

Subagents:
- backend
- frontend
- database
- QA
- security
- UX
- code review
- release

Use worktree isolation for parallel editing tasks.
