---
name: backend-implementation
description: Implement .NET 8 backend features for the metadata-driven platform.
---

1. Read the HLD/LLD sections relevant to the task.
2. Inspect existing interfaces and implementations.
3. Preserve dependency direction.
4. Use Dapper/Npgsql and parameterized values.
5. Resolve dynamic table/column identifiers only from trusted metadata.
6. Apply tenant/org/security centrally.
7. Keep domain/business rules outside generated code.
8. Add unit and integration tests.
9. Run formatter/build/tests.
10. Report deviations and risks.
