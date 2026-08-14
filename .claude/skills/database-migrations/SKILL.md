---
name: database-migrations
description: Create and review safe PostgreSQL migrations for the platform.
---

Rules:
- idempotent where the selected migration tool supports it
- explicit constraints and indexes
- avoid destructive changes in one step when compatibility can be preserved
- account for existing data
- document backfill strategy
- consider locks and production duration
- verify tenant/org indexes
- test upgrade from previous schema
- test rollback only if the selected strategy officially supports rollback; otherwise provide forward recovery
