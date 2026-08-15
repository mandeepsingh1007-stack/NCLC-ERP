# ADR-0007: Filter DSL

- **ID**: ADR-0007
- **Status**: Proposed
- **Date**: 2026-08-15
- **Context**: Phase 3 UI needs a filter DSL for generic grid filtering. Users apply filters to data grids via `/api/data/{table}?filter=...`. The HLD/LLD Section 34 Item 28 requires "Implement filter-logic evaluation for grid views." The filter DSL must produce parameterized SQL, prevent SQL injection, and be consistent between frontend (React/TypeScript) and backend (C#).

## Problem

How do we define a filter DSL that:
- Produces only parameterized SQL (never string concatenation for values)
- Prevents SQL injection at every layer
- Supports common operators (eq, ne, gt, gte, lt, lte, like, ilike, in, not in, between, notnull, null)
- Supports nested boolean logic (AND, OR, NOT)
- Is parseable from both frontend (TypeScript) and backend (C#)
- Has a clear error contract for invalid filters
- Validates field names against metadata before SQL generation
- Handles special characters in filter values safely

## Decision

Use a **JSON-based filter AST** parsed on the frontend or backend, validated against metadata, then consumed by the QueryBuilder to generate parameterized SQL.

### Filter AST Structure

```typescript
interface FilterNode {
  type: 'boolean' | 'predicate';
  op?: '$and' | '$or' | '$not';       // for boolean nodes
  clauses?: FilterNode[];               // for boolean nodes
  column?: string;                      // for predicate nodes
  op?: string;                          // operator name
  value?: unknown;                      // single value
  values?: unknown[];                   // multi-value (between, in)
}
```

### Operators

| Operator | JSON Key | SQL Generated | Value Type | Example |
|---|---|---|---|---|
| Equal | `eq` | `= @p` | single | `{ "column": "Status", "op": "eq", "value": "Active" }` |
| Not Equal | `ne` | `!= @p` | single | `{ "column": "Status", "op": "ne", "value": "Deleted" }` |
| Greater Than | `gt` | `> @p` | single | `{ "column": "Amount", "op": "gt", "value": 1000 }` |
| Greater Than Or Equal | `gte` | `>= @p` | single | `{ "column": "Amount", "op": "gte", "value": 1000 }` |
| Less Than | `lt` | `< @p` | single | `{ "column": "Qty", "op": "lt", "value": 100 }` |
| Less Than Or Equal | `lte` | `<= @p` | single | `{ "column": "Qty", "op": "lte", "value": 100 }` |
| Like (case-sensitive) | `like` | `LIKE @p` | single | `{ "column": "Name", "op": "like", "value": "%test%" }` |
| ILike (case-insensitive) | `ilike` | `ILIKE @p` | single | `{ "column": "Name", "op": "ilike", "value": "test%" }` |
| In | `in` | `IN (@p1, @p2, ...)` | array | `{ "column": "Status", "op": "in", "values": ["Active", "Pending"] }` |
| Not In | `not in` | `NOT IN (@p1, @p2, ...)` | array | `{ "column": "Status", "op": "not in", "values": ["Archived", "Deleted"] }` |
| Between | `between` | `BETWEEN @p1 AND @p2` | array [2] | `{ "column": "Amount", "op": "between", "values": [100, 1000] }` |
| Not Null | `notnull` | `IS NOT NULL` | — | `{ "column": "OptionalField", "op": "notnull" }` |
| Null | `null` | `IS NULL` | — | `{ "column": "OptionalField", "op": "null" }` |

### Boolean Logic (nested)

```json
{
  "type": "boolean",
  "op": "$and",
  "clauses": [
    { "column": "Status", "op": "eq", "value": "Active" },
    {
      "type": "boolean",
      "op": "$or",
      "clauses": [
        { "column": "Amount", "op": "gt", "value": 1000 },
        { "column": "Priority", "op": "eq", "value": "High" }
      ]
    }
  ]
}
```

Equivalent to: `Status = @p1 AND (Amount > @p2 OR Priority = @p3)`

### Operator Naming Convention

- Top-level filter keys use `$` prefix for boolean operators: `$and`, `$or`, `$not`
- Column operators use lowercase: `eq`, `ne`, `gt`, `like`, `in`, etc.
- `$and` and `$or` accept arrays of clauses (implicit AND when multiple clauses at same level)
- `$not` negates a single clause or boolean group

### Compact notation (top-level)

The API accepts filter as a JSON string in the query parameter:

```
GET /api/data/Users?filter={"column":"Status","op":"eq","value":"Active"}
GET /api/data/Users?filter={"type":"boolean","op":"$and","clauses":[...]}
```

Or the API can accept the compact object notation at the top level without the wrapping `{"type":"boolean"}`:

```json
{
  "Status": { "op": "eq", "value": "Active" }
}
```

This compact form is implicitly wrapped as AND across all keys. The full `{"type":"boolean", ...}` form is required for nested boolean logic.

## Parsing & Validation Flow

```
HTTP request with filter JSON string
    ↓
Parse JSON → Filter AST (dynamic/JsonElement)
    ↓
Validate AST structure (type, op, column, value)
    ↓
    → Invalid structure → 400 Bad Request
    ↓
Validate column name against SysColumn metadata for the table
    ↓
    → Unknown column → 400 Bad Request
    ↓
Validate operator against allowed operators for column type
    ↓
    → Invalid operator for type → 400 Bad Request
    ↓
Validate value type matches column data type
    ↓
    → Type mismatch → 400 Bad Request
    ↓
QueryBuilder.BuildSelect() → parameterized SQL + NpgsqlParameter[]
    ↓
Execute via Dapper
```

### Validation Rules

1. **Column must exist** in SysColumn metadata for the requested table
2. **Operator must be valid** for the column's .NET type:
   - `like`/`ilike`: only on string types (varchar, text, nvarchar)
   - `gt`/`gte`/`lt`/`lte`: only on numeric/date types
   - `in`/`not in`: value must be an array
   - `between`: value must be an array of exactly 2 elements
   - `null`/`notnull`: no value field required
3. **Value must be parameterized** — all values become NpgsqlParameter[]
4. **AST depth limit**: max 10 levels of nesting
5. **Clause count limit**: max 50 clauses per filter AST
6. **Max filter string length**: 4096 characters (query param limit)

## Security Implications

### SQL Injection Defense (3 Layers)

1. **Column allowlist**: Column names validated against SysColumn metadata before SQL generation. Unknown column = 400.
2. **Operator allowlist**: Only known operator keys are recognized. Unknown keys = 400.
3. **Parameterized values**: All values become `NpgsqlParameter[]`. No string interpolation for values.

### Like/ILike Wildcard Safety

- User-provided LIKE values are passed as-is (the user controls their own filter).
- The QueryBuilder does NOT add wildcards — the user includes `%` if desired.
- This is NOT a server-side search — it's a client-driven filter on already-validated columns.

### AST Depth & Count Limits

- Max 10 levels of nesting prevents stack overflow during AST traversal.
- Max 50 clauses prevents CPU DoS from overly complex filters.
- Max 4096-char filter string prevents query string DoS.

### No Raw SQL Input

- The filter AST is NEVER passed directly to SQL.
- The AST is parsed, validated, and then the QueryBuilder generates SQL from its own templates.
- The AST only specifies: which column, which operator, which value(s).

## Performance Implications

- **Parse time**: ~0.05ms for typical filter (5 clauses)
- **Validate time**: ~0.1ms (column lookup in metadata dictionary)
- **SQL generation time**: ~0.2ms (QueryBuilder template assembly)
- **Parameter binding**: ~0.05ms (NpgsqlParameter array creation)
- **Total overhead per request**: ~0.4ms
- **Caching**: SQL template (without values) can be cached per (table, filter-hash) combination

## Error Handling

| Error | HTTP Status | Response |
|---|---|---|
| Invalid JSON | 400 | `{ "error": "Invalid filter JSON", "details": "Expected { or [" }` |
| Unknown top-level key | 400 | `{ "error": "Invalid filter syntax", "details": "Use 'column' + 'op' or {'type':'boolean',...}" }` |
| Unknown column | 400 | `{ "error": "Unknown column", "details": "column 'foo' not found in table 'Books'" }` |
| Invalid operator | 400 | `{ "error": "Invalid operator", "details": "'foo' is not a valid filter operator" }` |
| Type mismatch | 400 | `{ "error": "Type mismatch", "details": "Column 'Amount' is numeric, expected numeric value" }` |
| AST too deep | 400 | `{ "error": "Filter too complex", "details": "Max nesting depth is 10" }` |
| Too many clauses | 400 | `{ "error": "Filter too complex", "details": "Max 50 clauses allowed" }` |

### Error Response Contract

All API errors follow the unified format:

```json
{
  "error": {
    "code": "INVALID_FILTER_COLUMN",
    "message": "column 'foo' not found in table 'Books'",
    "status": 400
  }
}
```

## Comparison with Display Logic (ADR-0006)

| Aspect | Display Logic (ADR-0006) | Filter DSL (ADR-0007) |
|---|---|---|
| Purpose | Conditional visibility/read-only/mandatory | Row-level data filtering |
| Operator set | Boolean: `&&`, `||`, `!`, `==`, `!=`, `like`, `empty` | SQL-like: `eq`, `ne`, `gt`, `like`, `in`, `between`, `null` |
| Syntax | Infix DSL string | JSON AST object |
| Parsing | Recursive descent parser | JSON deserializer (System.Text.Json) |
| Output | Boolean (true/false) | SQL WHERE clause + parameters |
| Storage | SysField.DisplayLogic (VARCHAR) | Query parameter (stateless, not stored) |
| Security | No eval(), depth 20, tokens 200 | Column allowlist, parameterized, depth 10, clauses 50 |
| Error default | Expression = false (hide field) | HTTP 400 with error details |

## Alternatives Considered

### String-based Filter ("Status == 'Active' && Amount > 1000")
- **Pros**: Intuitive for users
- **Cons**: Parsing complexity, SQL injection risk if not carefully handled, inconsistent operator syntax across teams

### MongoDB-style Query Documents (CHOSEN for concept, adapted)
- **Pros**: Well-known pattern, structured, easily validated
- **Cons**: Can be verbose for simple filters
- **Adaptation**: Simplified operator names (`eq` vs `$eq`), compact top-level notation for simple filters

### URL Query Parameters (?status=Active&amount_gt=1000)
- **Pros**: RESTful, no JSON parsing
- **Cons**: No nested boolean logic, hard to express OR/NOT, limited to simple AND chains

### GraphQL/REST-ish Query Language
- **Pros**: Powerful, typed
- **Cons**: Overkill for grid filtering, requires schema generation, not suitable for dynamic tables

## JSON AST Parser (C#)

```csharp
class FilterParser {
    // Parse JSON string → JsonElement AST
    JsonElement Parse(string filterJson);

    // Validate AST against table metadata
    ValidatedFilter Validate(JsonElement ast, TableMetadata table);
}

class ValidatedFilter {
    string SqlWhereClause { get; }  // "WHERE Status = @p1 AND Amount > @p2"
    NpgsqlParameter[] Parameters { get; }  // [("@p1", "Active"), ("@p2", 1000)]
    int ClauseCount { get; }  // 2
}
```

### TypeScript Filter Parser (Frontend)

```typescript
interface FilterClause {
  column: string;
  op: 'eq' | 'ne' | 'gt' | 'gte' | 'lt' | 'lte' | 'like' | 'ilike' | 'in' | 'not in' | 'between' | 'notnull' | 'null';
  value?: unknown;
  values?: unknown[];
}

interface FilterBooleanNode {
  type: 'boolean';
  op: '$and' | '$or' | '$not';
  clauses: (FilterClause | FilterBooleanNode)[];
}

type FilterAST = FilterClause | FilterBooleanNode;

function buildFilterString(ast: FilterAST): string {
  // Convert AST to JSON string for API query parameter
}

function parseFilterString(filterJson: string): FilterAST {
  // Parse JSON string to AST for UI filter builder
}
```

## Security Implications

- **Column injection**: Column names validated against SysColumn metadata → unknown column = 400
- **Operator injection**: Operator keys validated against allowlist → unknown operator = 400
- **Value injection**: All values parameterized via NpgsqlParameter → SQL injection impossible
- **AST injection**: AST depth and clause count limits prevent DoS
- **String length**: 4096-char limit on filter query parameter prevents DoS
- **Like wildcard**: User controls their own filter wildcards — not a server-side attack vector
- **Type validation**: Value type checked against column type before SQL generation

## UX Implications

- **Grid filter UI**: Visual filter builder (column dropdown + operator dropdown + value input + AND/OR toggle)
- **Advanced filter**: JSON editor for power users (raw filter AST)
- **Filter persistence**: Saved filters stored as metadata (future, Phase 5+)
- **Filter state**: TanStack Query key includes filter JSON → automatic re-fetch on filter change
- **Filter reset**: Clear button resets all filters → full table fetch
- **Filter validation**: Real-time validation in UI before sending to API
- **Error display**: Filter errors shown inline in grid (red banner above data)

## Backward Compatibility

- N/A — new feature, no existing filter system.
- Filter parameter is optional in `/api/data/{table}`. No filter = no WHERE clause = all rows.
- Adding new operators does not break existing filter requests.

## Testing Implications

- **Unit tests**: Filter parser (valid/invalid JSON, all operators), validator (column allowlist, operator allowlist, type checks), limit enforcement (depth, clauses, string length)
- **Security tests**: SQL injection via column name, operator name, value, nested AST, LIKE with special chars
- **Integration tests**: Filter in `/api/data/{table}` — correct rows returned, wrong rows excluded
- **Frontend tests**: Filter builder generates valid JSON AST, filter string round-trips through parse/build

## Migration Implications

- N/A — filter logic is runtime, no schema changes required.
- Works with existing sys_table and sys_column metadata.

## References

- HLD/LLD Section 34, Item 28: Implement filter-logic evaluation for grid views
- HLD/LLD Section 8: UI Metadata — Filter logic
- ADR-0006: Display Logic Grammar (similar security model, different output)
- CLAUDE.md Rule 7: All values must be parameterized
- CLAUDE.md Rule 17: Dynamic SQL identifiers must come only from trusted metadata
