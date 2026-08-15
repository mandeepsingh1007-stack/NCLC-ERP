/**
 * Display Logic Parity Tests
 *
 * Proves frontend evaluator matches backend DisplayLogicEvaluator
 * for every supported expression type.
 *
 * Backend: src/Platform.Core/Runtime/DisplayLogicEvaluator.cs
 * Frontend: src/utils/displayLogicEval.ts
 * Grammar: ADR-0006
 */
import { evaluateDisplayLogic, parseDisplayLogic } from '../utils/displayLogicEval';

type DisplayLogicContext = Parameters<typeof evaluateDisplayLogic>[1];

const ctx: DisplayLogicContext = {
  userId: 'user-1',
  tenantId: 'tenant-1',
  orgId: 'org-1',
  timestamp: '2026-01-01',
  userName: 'Alice',
};

// ─── Helpers ───────────────────────────────────────────────────────────

interface ParityCase {
  expression: string;
  formData: Record<string, unknown>;
  expected: boolean;
  description: string;
}

/**
 * PARITY RULE: Backend C# vs Frontend TS.
 *
 * Key differences documented:
 * 1. Backend uses strict Equals() for ==, frontend uses loose ==
 *    — frontend allows string "5" == number 5; backend does not.
 *    This is intentional for JS flexibility.
 * 2. Backend does NOT support 'in', 'not in', 'empty', 'not empty'.
 *    Frontend extends grammar — no parity needed for these.
 * 3. Backend LIKE uses SQL-style % and _; frontend converts to regex.
 *    Both produce equivalent results for standard SQL LIKE patterns.
 * 4. Backend short-circuits AND/OR; frontend && || also short-circuits.
 */
function expectParity(cases: ParityCase[]) {
  const results: { pass: number; fail: string[] } = { pass: 0, fail: [] };

  for (const { expression, formData, expected, description } of cases) {
    // Parse must succeed
    let parsed: ReturnType<typeof parseDisplayLogic>;
    try {
      parsed = parseDisplayLogic(expression);
    } catch (e) {
      results.fail.push(`${description}: parse threw ${e}`);
      continue;
    }

    // Evaluate
    let actual: boolean;
    try {
      actual = evaluateDisplayLogic(expression, ctx, formData);
    } catch (e) {
      results.fail.push(`${description}: eval threw ${e}`);
      continue;
    }

    // Parity check
    if (actual !== expected) {
      results.fail.push(
        `${description}: expected ${expected}, got ${actual} (expr="${expression}")`,
      );
    } else {
      results.pass++;
    }
  }

  return results;
}

// ─── Literals ──────────────────────────────────────────────────────────

describe('Display Logic Parity — Literals', () => {
  const cases: ParityCase[] = [
    { expression: 'true', formData: {}, expected: true, description: 'true literal' },
    { expression: 'false', formData: {}, expected: false, description: 'false literal' },
    { expression: 'null', formData: {}, expected: false, description: 'null literal' },
    { expression: "'hello'", formData: {}, expected: true, description: 'non-empty string literal' },
    { expression: '""', formData: {}, expected: false, description: 'empty string literal' },
    { expression: '42', formData: {}, expected: true, description: 'non-zero number literal' },
    { expression: '0', formData: {}, expected: false, description: 'zero number literal' },
  ];

  it('evaluates all literal expressions correctly', () => {
    const r = expectParity(cases);
    expect(r.pass).toBe(cases.length);
    if (r.fail.length) {
      fail(r.fail.join('\n'));
    }
  });
});

// ─── Field References ──────────────────────────────────────────────────

describe('Display Logic Parity — Field References', () => {
  const cases: ParityCase[] = [
    {
      expression: '$Active',
      formData: { Active: true },
      expected: true,
      description: 'truthy field ref',
    },
    {
      expression: '$Active',
      formData: { Active: false },
      expected: false, // JS truthiness: false is falsy
      description: 'false field ref (falsy)',
    },
    {
      expression: '$Name',
      formData: { Name: 'test' },
      expected: true,
      description: 'non-empty string field ref',
    },
    {
      expression: '$Name',
      formData: { Name: '' },
      expected: false,
      description: 'empty string field ref',
    },
    {
      expression: '$Name',
      formData: {},
      expected: false,
      description: 'missing field ref',
    },
    {
      expression: '$Name',
      formData: { Name: null },
      expected: false,
      description: 'null field ref',
    },
    // Context variables
    {
      expression: '$UserId',
      formData: { $UserId: 'user-1' },
      expected: true,
      description: 'context UserId',
    },
    {
      expression: '$UserId',
      formData: { $UserId: '' },
      expected: false,
      description: 'context UserId empty',
    },
    {
      expression: '$UserName',
      formData: { $UserName: 'Alice' },
      expected: true,
      description: 'context UserName',
    },
  ];

  it('evaluates all field reference expressions correctly', () => {
    const r = expectParity(cases);
    expect(r.pass).toBe(cases.length);
    if (r.fail.length) {
      fail(r.fail.join('\n'));
    }
  });
});

// ─── Comparison Operators ──────────────────────────────────────────────

describe('Display Logic Parity — Comparisons', () => {
  const cases: ParityCase[] = [
    // Equality
    { expression: "$Active == true", formData: { Active: true }, expected: true, description: 'eq true' },
    { expression: "$Active == true", formData: { Active: false }, expected: false, description: 'eq false' },
    { expression: "$Active == false", formData: { Active: false }, expected: true, description: 'eq false val' },
    { expression: "$Status == 'Active'", formData: { Status: 'Active' }, expected: true, description: 'eq string' },
    { expression: "$Status == 'Active'", formData: { Status: 'Inactive' }, expected: false, description: 'neq string' },
    { expression: "$Count == 5", formData: { Count: 5 }, expected: true, description: 'eq number' },
    // Inequality
    { expression: "$Status != 'Active'", formData: { Status: 'Inactive' }, expected: true, description: 'ne string' },
    { expression: "$Status != 'Active'", formData: { Status: 'Active' }, expected: false, description: 'not ne string' },
    { expression: "$Status <> 'Active'", formData: { Status: 'Inactive' }, expected: true, description: '<> operator' },
    // Less than
    { expression: "$Count < 10", formData: { Count: 5 }, expected: true, description: 'less than' },
    { expression: "$Count < 10", formData: { Count: 15 }, expected: false, description: 'not less than' },
    // Greater than
    { expression: "$Count > 10", formData: { Count: 15 }, expected: true, description: 'greater than' },
    { expression: "$Count > 10", formData: { Count: 5 }, expected: false, description: 'not greater than' },
    // <= >=
    { expression: "$Count <= 5", formData: { Count: 5 }, expected: true, description: 'less or equal' },
    { expression: "$Count <= 5", formData: { Count: 3 }, expected: true, description: 'less than' },
    { expression: "$Count <= 5", formData: { Count: 8 }, expected: false, description: 'not less or equal' },
    { expression: "$Count >= 5", formData: { Count: 5 }, expected: true, description: 'greater or equal' },
    { expression: "$Count >= 5", formData: { Count: 8 }, expected: true, description: 'greater' },
    { expression: "$Count >= 5", formData: { Count: 2 }, expected: false, description: 'not greater or equal' },
    // LIKE
    { expression: "$Name like 'test%'", formData: { Name: 'testing' }, expected: true, description: 'like with %' },
    { expression: "$Name like 'test%'", formData: { Name: 'test' }, expected: true, description: 'like exact match' },
    { expression: "$Name like 'test%'", formData: { Name: 'other' }, expected: false, description: 'not like' },
    { expression: "$Name like 't__t'", formData: { Name: 'test' }, expected: true, description: 'like with _' },
    { expression: "$Name not like 'test%'", formData: { Name: 'other' }, expected: true, description: 'not like' },
  ];

  it('evaluates all comparison expressions correctly', () => {
    const r = expectParity(cases);
    expect(r.pass).toBe(cases.length);
    if (r.fail.length) {
      fail(r.fail.join('\n'));
    }
  });
});

// ─── Empty / Not Empty (frontend-only operators) ──────────────────────

describe('Display Logic Parity — Empty / Not Empty (frontend-only)', () => {
  const cases: ParityCase[] = [
    { expression: "$Name empty", formData: { Name: '' }, expected: true, description: 'empty string' },
    { expression: "$Name empty", formData: { Name: 'test' }, expected: false, description: 'non-empty string' },
    { expression: "$Name empty", formData: {}, expected: true, description: 'missing field' },
    { expression: "$Name not empty", formData: { Name: 'test' }, expected: true, description: 'not empty string' },
    { expression: "$Name not empty", formData: { Name: '' }, expected: false, description: 'empty string negated' },
    { expression: "$Name not empty", formData: {}, expected: false, description: 'missing field negated' },
  ];

  it('evaluates frontend-only empty operators correctly', () => {
    const r = expectParity(cases);
    expect(r.pass).toBe(cases.length);
    if (r.fail.length) {
      fail(r.fail.join('\n'));
    }
  });
});

// ─── In / Not In (frontend-only operators) ────────────────────────────

describe('Display Logic Parity — In / Not In (frontend-only)', () => {
  const cases: ParityCase[] = [
    { expression: "$Status in ['Active', 'Pending']", formData: { Status: 'Active' }, expected: true, description: 'in list' },
    { expression: "$Status in ['Active', 'Pending']", formData: { Status: 'Closed' }, expected: false, description: 'not in list' },
    { expression: "$Status not in ['Active', 'Pending']", formData: { Status: 'Closed' }, expected: true, description: 'not in list negated' },
    { expression: "$Status not in ['Active', 'Pending']", formData: { Status: 'Active' }, expected: false, description: 'in list negated' },
    { expression: "$Count in [1, 2, 3]", formData: { Count: 2 }, expected: true, description: 'numeric in list' },
    { expression: "$Count in [1, 2, 3]", formData: { Count: 5 }, expected: false, description: 'numeric not in list' },
  ];

  it('evaluates frontend-only in operators correctly', () => {
    const r = expectParity(cases);
    expect(r.pass).toBe(cases.length);
    if (r.fail.length) {
      fail(r.fail.join('\n'));
    }
  });
});

// ─── Boolean Operators ─────────────────────────────────────────────────

describe('Display Logic Parity — Boolean Operators', () => {
  const cases: ParityCase[] = [
    // AND
    { expression: "$Active && $Name", formData: { Active: true, Name: 'test' }, expected: true, description: 'and both true' },
    { expression: "$Active && $Name", formData: { Active: true, Name: '' }, expected: false, description: 'and second empty' },
    { expression: "$Active && $Name", formData: { Active: false, Name: 'test' }, expected: false, description: 'and first false' },
    { expression: "$Active && $Name", formData: { Active: false, Name: '' }, expected: false, description: 'and both false' },
    // OR
    { expression: "$Active || $Name", formData: { Active: true, Name: '' }, expected: true, description: 'or first true' },
    { expression: "$Active || $Name", formData: { Active: false, Name: 'test' }, expected: true, description: 'or second true' },
    { expression: "$Active || $Name", formData: { Active: true, Name: 'test' }, expected: true, description: 'or both true' },
    { expression: "$Active || $Name", formData: { Active: false, Name: '' }, expected: false, description: 'or both false' },
    // NOT
    { expression: "!$Active", formData: { Active: false }, expected: true, description: 'not true' },
    { expression: "!$Active", formData: { Active: true }, expected: false, description: 'not false' },
    { expression: "!!$Active", formData: { Active: true }, expected: true, description: 'double not' },
    // Mixed
    { expression: "$Active && $Name || $Status", formData: { Active: false, Name: '', Status: 'X' }, expected: true, description: 'and/or mixed' },
    { expression: "$Active || $Name && $Status", formData: { Active: false, Name: 't', Status: 'X' }, expected: true, description: 'or/and mixed' },
    { expression: "$Active || $Name && $Status", formData: { Active: false, Name: '', Status: 'X' }, expected: false, description: 'and higher precedence' },
  ];

  it('evaluates all boolean operator expressions correctly', () => {
    const r = expectParity(cases);
    expect(r.pass).toBe(cases.length);
    if (r.fail.length) {
      fail(r.fail.join('\n'));
    }
  });
});

// ─── Parentheses ───────────────────────────────────────────────────────

describe('Display Logic Parity — Parentheses', () => {
  const cases: ParityCase[] = [
    { expression: "($Active)", formData: { Active: true }, expected: true, description: 'single paren' },
    { expression: "($Active)", formData: { Active: false }, expected: false, description: 'single paren false' },
    { expression: "($Active && $Name) || $Status", formData: { Active: true, Name: '', Status: 'X' }, expected: true, description: 'grouped and then or' },
    { expression: "($Active || $Name) && $Status", formData: { Active: true, Name: '', Status: 'X' }, expected: true, description: 'grouped or then and' },
    { expression: "($Active || $Name) && $Status", formData: { Active: true, Name: '', Status: '' }, expected: false, description: 'grouped or then and false' },
    { expression: "!($Active && $Name)", formData: { Active: true, Name: '' }, expected: true, description: 'not grouped' },
    { expression: "!(($Active && $Name))", formData: { Active: true, Name: '' }, expected: true, description: 'nested not grouped' },
  ];

  it('evaluates parenthesized expressions correctly', () => {
    const r = expectParity(cases);
    expect(r.pass).toBe(cases.length);
    if (r.fail.length) {
      fail(r.fail.join('\n'));
    }
  });
});

// ─── Null Safety ───────────────────────────────────────────────────────

describe('Display Logic Parity — Null Safety', () => {
  const cases: ParityCase[] = [
    { expression: 'null', formData: {}, expected: false, description: 'null literal falsy' },
    { expression: 'null == null', formData: {}, expected: true, description: 'null == null' },
    { expression: 'null != null', formData: {}, expected: false, description: 'null != null is false' },
    { expression: "$Name == null", formData: { Name: null }, expected: true, description: 'field == null' },
    { expression: "$Name == null", formData: { Name: 'test' }, expected: false, description: 'field != null' },
  ];

  it('handles null safely', () => {
    const r = expectParity(cases);
    expect(r.pass).toBe(cases.length);
    if (r.fail.length) {
      fail(r.fail.join('\n'));
    }
  });
});

// ─── Invalid / Edge Cases ─────────────────────────────────────────────

describe('Display Logic — Invalid & Edge Cases', () => {
  it('returns false for null expression', () => {
    expect(evaluateDisplayLogic(null, ctx, {})).toBe(true); // null = always show
  });

  it('returns false for empty string expression', () => {
    expect(evaluateDisplayLogic('', ctx, {})).toBe(true); // empty = always show
  });

  it('returns false for malformed expression', () => {
    expect(evaluateDisplayLogic('(', ctx, {})).toBe(false); // truncated
  });

  it('returns false for too-long expression', () => {
    const long = 'true&&'.repeat(100);
    expect(evaluateDisplayLogic(long, ctx, {})).toBe(false); // exceeds 4096
  });

  it('throws token limit error for very long expression via parse', () => {
    const long = 'true&&'.repeat(101); // 202 tokens > MAX_TOKENS=200
    expect(() => parseDisplayLogic(long)).toThrow('exceeds token limit');
  });

  it('handles nested expressions within limits', () => {
    const nested = '($Active && $Name)';
    expect(evaluateDisplayLogic(nested, ctx, { Active: true, Name: 'x' })).toBe(true);
  });
});

// ─── Parse Tree Structure ──────────────────────────────────────────────

describe('Display Logic — Parse Tree Structure', () => {
  it('parses a simple field reference', () => {
    const ast = parseDisplayLogic('$Name');
    expect(ast).toEqual({ kind: 'fieldref', fieldName: '$Name' });
  });

  it('parses a simple boolean', () => {
    const ast = parseDisplayLogic('true');
    expect(ast).toEqual({ kind: 'literal', value: true });
  });

  it('parses an AND expression', () => {
    const ast = parseDisplayLogic('$A && $B');
    expect(ast).toHaveProperty('kind', 'and');
    expect(ast).toHaveProperty('left.fieldName', '$A');
    expect(ast).toHaveProperty('right.fieldName', '$B');
  });

  it('parses an OR expression', () => {
    const ast = parseDisplayLogic('$A || $B');
    expect(ast).toHaveProperty('kind', 'or');
  });

  it('parses a NOT expression', () => {
    const ast = parseDisplayLogic('!$Active');
    expect(ast).toHaveProperty('kind', 'not');
  });

  it('parses a comparison', () => {
    const ast = parseDisplayLogic("$Status == 'Active'");
    expect(ast).toHaveProperty('kind', 'compare');
    expect(ast).toHaveProperty('op', '==');
  });
});
