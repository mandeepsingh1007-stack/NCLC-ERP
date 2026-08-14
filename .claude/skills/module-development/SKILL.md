---
name: module-development
description: Build an installable metadata-driven module following module manifest, migration, dictionary ownership and registration rules.
---

Validate:
- manifest
- version/dependencies
- migrations
- dictionary seed/update ownership
- generated X_<Table>
- M_<Table> business logic
- validators/callouts
- processes/workflows
- install/upgrade/rollback or forward-recovery behavior
- end-to-end install and standard CRUD without custom React CRUD screens
