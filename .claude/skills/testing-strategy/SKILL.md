---
name: testing-strategy
description: Apply the platform's complete testing pyramid.
---

Required layers:
- unit
- integration with real PostgreSQL where behavior depends on DB semantics
- API contract
- E2E for critical user journeys
- security/regression tests

For metadata-driven behavior include generated/runtime combinations.
For authorization include positive and negative cases.
For multi-tenancy prove cross-tenant access is rejected.
For cache invalidation prove stale metadata is not served after commit.
