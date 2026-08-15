# Test Baseline — No-Code/Low-Code Platform

## Overview

Authoritative test counts by project, phase, and category. All counts verified via `dotnet test --list-tests`.

## Phase 0 Baseline

| Project | Test Count | Description |
|---------|-----------|-------------|
| Platform.Tests.Core | 2 | PlatformTests — TestId uniqueness (empty) |

**Total: 2 tests** — No meaningful assertions, just scaffolding.

## Phase 1 Baseline

### Core Unit Tests — Platform.Tests.Core

| Category | Count | Files |
|----------|-------|-------|
| Model tests | 22 | SysColumnTests (4), SysElementTests (3), SysReferenceListTests (3), SysReferenceTests (3), SysTableTests (3), SysTranslationTests (3), SysValRuleTests (3) |

**Total: 22 tests, 22 passing**

### Schema Contract Tests — Platform.Tests.SchemaContract

| Category | Count | Description |
|----------|-------|-------------|
| SysElement | 5 | Column count, type, uniqueness, default, composite PK |
| SysElement_Trl | 2 | Composite PK, language max length |
| SysReference | 3 | Name uniqueness, validation type |
| SysReferenceList | 3 | Composite+unique constraint, value/name max length |
| SysReferenceTable | 4 | Columns, FK, key column max length |
| SysValRule | 4 | Name uniqueness, rule type default, code max length |
| SysTable | 3 | Name uniqueness, access level default, entity type default |
| SysColumn | 7 | Column count, FKs, max length, uniqueness |
| General | 1 | All 8 tables exist |

**Total: 33 tests, 33 passing**

### Integration Tests — Platform.Tests.Integration

| Test | Description |
|------|-------------|
| All_8_Dictionary_Tables_Should_Exist | DDL migration verification |
| SysColumn_ShouldHave_All_Required_Columns | Column DDL verification |
| SysTable_ShouldHave_All_Required_Columns | Column DDL verification |
| SysReference_Should_Be_Seeded | Seed count >= 11 |
| SysValRule_Should_Be_Seeded | Seed count >= 2 |
| SysTable_Should_Be_Seeded | Seed count >= 7 |
| SysElement_Should_Be_Seeded | Seed count >= 27 |
| SysReferenceTable_Should_Be_Seeded | Seed count >= 1 |
| ForeignKeys_Should_Have_No_Orphans | FK integrity check |
| UNIQUE_Constraints_Should_Be_Enforced | 23505 enforcement |

**Total: 10 tests, 10 passing (require Docker/PostgreSQL)**

### Phase 1 Totals

| Category | Count |
|----------|-------|
| Core unit tests | 22 |
| Schema contract tests | 33 |
| Integration tests | 10 |
| **Total** | **65** |

## Phase 2 Planned

### Unit Tests (45 tests)

| Component | Test Count | Target File |
|-----------|-----------|-------------|
| MetadataGraph | 6 | MetadataGraphTests.cs |
| MetadataCacheService | 8 | MetadataCacheServiceTests.cs |
| CacheInvalidationService | 5 | CacheInvalidationServiceTests.cs |
| TypeValidator | 6 | TypeValidatorTests.cs |
| ReferenceValueValidator | 6 | ReferenceValueValidatorTests.cs |
| ValRuleEngine | 6 | ValRuleEngineTests.cs |
| POValidator | 4 | POValidatorTests.cs |
| POLifecycleManager | 4 | POLifecycleManagerTests.cs |
| POFactory | 4 | POFactoryTests.cs |
| **Total** | **45** |

### Integration Tests (24 tests)

| Category | Test Count |
|----------|-----------|
| Cache + invalidation | 6 |
| Validation pipeline | 8 |
| PO lifecycle | 6 |
| Metadata loading | 4 |
| **Total** | **24** |

### Phase 2 Totals (planned)

| Category | Count |
|----------|-------|
| Unit tests | 45 |
| Integration tests | 24 |
| **Total** | **69** |

## Grand Totals (after Phase 2)

| Category | Count |
|----------|-------|
| Phase 1 tests | 65 |
| Phase 2 tests | 69 |
| **Total** | **134** |

## Notes

- Integration tests require Docker/PostgreSQL (local) or connection string env var (CI)
- Schema contract tests require PostgreSQL connection — run in CI or with real DB locally
- Platform.Tests.Core has 22 Phase 1 model tests + 2 empty scaffolding tests = 24 total listed
- The "136 tests" figure in the preflight test matrix was approximate; actual count is 65 for Phase 1
- Phase 2 unit tests have NOT been written yet — 45 tests are planned
