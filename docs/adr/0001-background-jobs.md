# ADR-001: Background Job Processing with Hangfire

- **Status**: Accepted
- **Date**: 2026-08-14
- **Context**: The platform needs background job processing for scheduled tasks, workflows, and async operations.

## Decision
We use **Hangfire** with **Hangfire.PostgreSql** for background job processing.

## Rationale
- Hangfire provides a built-in dashboard for monitoring jobs.
- Native PostgreSQL storage via `Hangfire.PostgreSql` avoids needing a separate Redis job store.
- Simpler operations model vs. Quartz.NET for our use case.
- Quartz.NET was considered but has more complexity in scheduling and doesn't integrate as seamlessly with ASP.NET Core DI.

## Consequences
- Hangfire dashboard is available at `/hangfire` (behind auth in production).
- Jobs must be serializable and compatible with .NET 8.
- PostgreSQL is used for both application data and job storage.
