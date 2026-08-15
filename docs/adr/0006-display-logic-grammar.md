# ADR-0006: Display Logic Expression Grammar

- **ID**: ADR-0006
- **Status**: Proposed
- **Date**: 2026-08-15
- **Context**: Phase 3 UI needs display-logic evaluation for conditional field visibility, read-only state, and mandatory state. Fields in generic forms (SysField) have displayLogic, readOnlyLogic, and mandatoryLogic columns. The HLD/LLD Section 34 Item 27 requires "Implement display-logic evaluation." The grammar must be safe, deterministic, and parseable on both frontend (React/TypeScript) and backend (C#).

## Problem

How do we define a safe, deterministic, metadata-driven boolean expression grammar that:
- Evaluates display logic stored in database metadata (SysField.DisplayLogic VARCHAR(500))
- Does NOT use eval(), Function(), or arbitrary JavaScript/SQL execution
- Supports boolean operators (AND, OR, NOT) and comparisons (==, !=, <, >, <=, >=)
- Handles null values gracefully
- Is parseable on both frontend (React) and backend (C#)
- Prevents injection attacks (SQL injection, code injection, XSS)
- Has execution limits to prevent DoS

## Decision

Use a **parsed expression tree** (recursive descent parser). Define a minimal DSL:

```
Expression  = OrExpr
OrExpr      = AndExpr ( '||' AndExpr )*
AndExpr     = NotExpr ( '&&' NotExpr )*
NotExpr     = '!' NotExpr | Primary
Primary     = '(' Expression ')'
            | FieldRef
            | Literal
            | Comparison

FieldRef    = '$' FieldName          -- e.g., $Status, $UserId
Literal     = StringLiteral          -- 'Active', "Draft"
            | NumberLiteral          -- 42, 3.14
            | 'true' | 'false'
            | 'null'

Comparison  = Primary CompOp Primary
CompOp      = '==' | '!=' | '<>' | '<' | '>' | '<=' | '>='
            | 'in' | 'not in'
            | 'like' | 'not like'
            | 'empty' | 'not empty'
```

### Field References

Field references resolve from runtime context and PO values:

| Reference | Resolves From | Example |
|---|---|---|
| `$UserId` | IReadOnlyContext.UserId | `$UserId` → "abc-123" |
| `$TenantId` | IReadOnlyContext.TenantId | `$TenantId` → "tenant-001" |
| `$OrgId` | IReadOnlyContext.OrgId | `$OrgId` → "org-100" |
| `$Timestamp` | IReadOnlyContext.Timestamp | `$Timestamp` → "2026-08-15T10:00:00Z" |
| `$UserName` | IReadOnlyContext.UserName | `$UserName` → "john.doe" |
| `$FieldName` | Current PO values | `$Status` → "Active", `$Amount` → 500 |

### Null Handling

- `null == null` → true
- `value op null` → false for all comparison ops (two-valued logic, no three-valued)
- `empty null` → true
- `empty ""` → true
- `empty " "` → false (space is not empty)

### Type Coercion

- Numbers compared as numbers
- Strings compared as strings
- Booleans compared as booleans
- Cross-type comparison → false (not an error, just false)
- `in`/`not in` requires same-type array on right side

### Security Restrictions

- NO function calls
- NO property access (no `obj.prop`)
- NO nested expressions beyond operator precedence
- MAX depth: 20 levels
- MAX tokens: 200 per expression
- NO eval, NO RegExp, NO string concatenation

### Execution Limits

- Parse tree max depth: 20
- Max token count: 200
- Evaluation timeout: 50ms (CancellationToken)
- No recursion beyond precedence levels

### Error Handling

- Parse errors → validation error (field-level), NOT an exception that crashes the request
- Evaluation errors → treat expression as false (conservative: hide field on error)
- Unknown field references → treat as null/false

## Alternatives Considered

### Simple String Conditions ("Field == Value")
- **Pros**: Simple, intuitive for business users
- **Cons**: No boolean operators, no grouping, too limited for complex display logic

### Full Expression Language (NCalc, Spring Expression)
- **Pros**: Powerful, many operators/functions
- **Cons**: Overkill for display logic, harder to audit, larger attack surface, different grammar on frontend vs backend

### JSON-Based Conditions (MongoDB-style queries)
- **Pros**: Structured, easily parsed
- **Cons**: Not intuitive for business users, verbose: `{"field": "Status", "op": "eq", "value": "Active"}`

### JavaScript eval on Frontend + Parsed on Backend
- **Pros**: Native JavaScript for frontend
- **Cons**: XSS risk, inconsistent behavior between frontend and backend, eval() is a security anti-pattern

### Parsed Expression Tree (CHOSEN)
- **Pros**: Same grammar on frontend + backend, safe (no eval), deterministic, audit-friendly, bounded (depth/token limits)
- **Cons**: Slightly more complex to implement than string conditions, limited operator set (by design)

## Decision Rationale

Parsed expression tree is the right choice because:

1. **Safety**: No eval(), no code execution. The parser only recognizes specific tokens and operators. Anything else is a parse error.
2. **Consistency**: Same grammar on frontend (TypeScript) and backend (C#) ensures identical evaluation results.
3. **Auditability**: The expression grammar is small and well-defined. Security review is straightforward.
4. **Bounded**: Depth and token limits prevent DoS. 20 levels of nesting is more than enough for any real display logic.
5. **Extensibility**: New operators (e.g., `regex` for pattern matching) can be added without changing the grammar core.

## Syntax Examples

### Valid Expressions

```
# Simple equality
Status == 'Active'

# Compound with AND
$Amount > 1000 && Priority == 'High'

# OR condition
Status == 'Draft' || Status == 'Pending'

# NOT condition
NOT(Status == 'Closed')

# Nested with parentheses
(Status == 'Draft' || Status == 'Pending') && !$ReviewerId

# Context variable comparison
$UserId != null && $ApproverId == $UserId

# Like operator
$Name like '%test%'

# Empty check
empty $OptionalField

# Not empty
not empty $OptionalField

# In list
$Status in ('Active', 'Pending', 'Review')

# Not in
$Status not in ('Archived', 'Deleted')

# Comparison operators
$Qty >= 10 && $Qty <= 100

# Nested parentheses
((Status == 'Draft') && ($Priority == 'High' || $Amount > 5000))
```

### Invalid Expressions

```
# SQL injection
Status == 'Active'; DROP TABLE users

# Code execution
eval('alert(1)')

# Property access
obj.property

# Function call
upper(Status)

# Too deep nesting (beyond 20 levels)
((((((((((((((((((((a))))))))))))))))))

# Token limit exceeded (200+ tokens)
a == 1 && a == 2 && ... (200+ times)
```

### Edge Cases

```
# Empty string comparison
$Field == ''        -- matches empty string
empty $Field        -- matches null or empty string
empty ' '           -- false (space is not empty)

# Numeric string comparison
$Field == "10"      -- string comparison (not numeric)
$Field == 10        -- numeric comparison

# Boolean vs string
$Field == true      -- boolean true
$Field == 'true'    -- string "true"

# Null comparison
$Field == null      -- true if field is null
$Field != null      -- true if field is not null
null == null        -- true
null == ''          -- false (null is not empty string)
```

### Type Mismatch

```
# Cross-type comparison → false (not an error)
"10" > 5            -- false (string "10" vs number 5)
"abc" == "def"      -- true (string comparison)
true == 1           -- false (boolean vs number)
```

## Security Implications

- **Injection prevention**: The parser only recognizes specific tokens. SQL fragments, JavaScript code, or any unrecognized syntax is a parse error.
- **No eval()**: The parser builds an AST. The evaluator traverses the AST. No code execution from strings.
- **Depth limit**: Max 20 levels prevents stack overflow DoS.
- **Token limit**: Max 200 tokens prevents CPU DoS from complex expressions.
- **Evaluation timeout**: 50ms timeout prevents runaway evaluation.
- **Conservative error handling**: Parse/evaluation errors → expression is false (field hidden). This is the safe default — hide on error, not show on error.
- **Field reference validation**: Field references (`$FieldName`) resolve from PO values, not from arbitrary user input. The field name is part of the metadata, not the user input.

## Performance Implications

- **Parse time**: ~0.1ms for typical expressions (20 tokens)
- **Evaluate time**: ~0.01ms per evaluation (AST traversal)
- **Caching**: Parsed AST is cached (by expression string hash) — parse once, evaluate many times
- **Frontend**: Same parser in TypeScript, ~0.1ms parse, ~0.01ms evaluate
- **Total per field**: ~0.2ms (parse + first evaluate) + ~0.01ms per subsequent evaluate
- **100 fields with display logic**: ~20ms initial parse + ~1ms per re-evaluation cycle

## UX Implications

- **Error visibility**: Invalid display logic in metadata → field hidden (user sees nothing unusual). Metadata designer should see a validation error when saving the field.
- **Conservative default**: On error, field is HIDDEN (not shown). This is safer but may confuse users if the display logic has a bug.
- **Metadata validation**: When saving SysField metadata, displayLogic is validated. Invalid expressions are rejected with a clear error message.
- **No visual indication of display logic**: Hidden fields are simply not rendered. Users don't see "this field is hidden by display logic."

## Backward Compatibility

- N/A — new feature, no existing data affected.
- SysField.DisplayLogic, SysField.ReadOnlyLogic, SysField.MandatoryLogic are nullable VARCHAR(500) columns. Null = no logic = always visible/editable/mandatory.

## Testing Implications

- **Unit tests**: Parser tests (valid/invalid expressions), evaluator tests (all operators, null handling, type coercion), limit tests (depth, tokens, timeout).
- **Security tests**: SQL injection attempts, eval() attempts, XSS attempts, deep nesting, token overflow.
- **Integration tests**: Display logic in generic form — field visibility matches expression evaluation.
- **Frontend tests**: Same expressions evaluated on frontend match backend results.

## Migration Implications

- New columns added in Migration 003: SysField.DisplayLogic, SysField.ReadOnlyLogic, SysField.MandatoryLogic
- All nullable, default NULL. Existing metadata (if any migrated from other systems) may have null values.
- No data migration needed.

## Future Extensibility

- **New operators**: `regex` for regex matching, `contains` for substring matching, `startsWith`/`endsWith` for string operations.
- **New functions**: `upper()`, `lower()`, `length()`, `today()`, `now()` — functions would need to be explicitly allowed in the parser.
- **Date operations**: `dateCompare`, `dateAdd`, `dateDiff` — for date-based display logic.
- **All additions**: Extend the operator/function allowlist without changing the grammar core.

## Consequences

### Because of this decision:

**Pros:**
- Safe: no eval(), no code execution from strings
- Deterministic: same input → same output, always
- Auditable: small, well-defined grammar
- Consistent: same grammar on frontend and backend
- Bounded: depth/token/timeout limits prevent DoS
- Extensible: operators and functions can be added

**Cons:**
- Limited operator set (by design — prevents complexity and attack surface)
- Not as intuitive as JavaScript for non-technical users (but display logic is authored by metadata designers, not end users)
- Must maintain two parsers (C# backend + TypeScript frontend) — but same grammar ensures consistency

## C# Implementation

```csharp
// AST node types
interface IExpressionNode { }
class AndNode : IExpressionNode { IExpressionNode Left { get; } IExpressionNode Right { get; } }
class OrNode : IExpressionNode { IExpressionNode Left { get; } IExpressionNode Right { get; } }
class NotNode : IExpressionNode { IExpressionNode Operand { get; } }
class CompareNode : IExpressionNode { string Operator { get; } IExpressionNode Left { get; } IExpressionNode Right { get; } }
class FieldRefNode : IExpressionNode { string FieldName { get; } }
class LiteralNode : IExpressionNode { object Value { get; } }

// Parser
class ExpressionParser {
    IExpressionNode Parse(string expression);  // Returns AST or throws ParseError
}

// Evaluator
class ExpressionEvaluator {
    bool Evaluate(IExpressionNode ast, IReadOnlyContext context, IDictionary<string, object?> poValues);
}
```

No external parsing libraries. Recursive descent parser, ~200 lines of code.

## References

- HLD/LLD Section 34, Item 27: Implement display-logic evaluation
- HLD/LLD Section 8: UI Metadata — Display logic
- CLAUDE.md Rule 5: UI is never the security boundary
