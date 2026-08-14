# ADR-003: Database Migration Strategy

- **Status**: Proposed
- **Date**: 2026-08-14
- **Context**: The platform needs a reliable, repeatable database migration system for PostgreSQL.

## Decision
Use **DbUp** for database migrations.

## Rationale
- DbUp is framework-agnostic and works well with raw SQL migrations.
- Supports versioned, incremental migrations stored as embedded resources.
- Simpler than FluentMigrator for our metadata-first approach where many tables are created dynamically.
- Raw SQL migrations align with the dictionary-first philosophy — metadata schemas are SQL, not code-generated.

## Alternatives Considered
- **FluentMigrator**: Better for code-first migrations with up/down methods. Less natural for SQL-heavy dictionary schema.
- **EF Core Migrations**: Adds EF Core dependency. We use Dapper, not EF Core.

## Consequences
- Migrations are SQL scripts, reviewed like any other DDL.
- Migration tracking table (`dbup`) is managed by DbUp automatically.
- Module migrations are handled separately in Phase 8.
