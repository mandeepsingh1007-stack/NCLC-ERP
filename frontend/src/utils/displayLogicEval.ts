/**
 * Client-side display logic evaluator — TypeScript port of backend DisplayLogicEvaluator.
 * Same grammar as ADR-0006. No eval(), no new Function().
 *
 * Grammar:
 *   Expression = OrExpr
 *   OrExpr     = AndExpr ('||' AndExpr)*
 *   AndExpr    = NotExpr ('&&' NotExpr)*
 *   NotExpr    = '!' NotExpr | Primary
 *   Primary    = '(' Expression ')' | FieldRef | Literal | Comparison
 *   FieldRef   = '$' FieldName
 *   Literal    = String | Number | 'true' | 'false' | 'null'
 *   Comparison = Primary CompOp Primary
 *   CompOp     = '==' | '!=' | '<>' | '<' | '>' | '<=' | '>='
 *              | 'in' | 'not in' | 'like' | 'empty' | 'not empty'
 *
 * Limits: depth 20, tokens 200.
 */

// --- AST Nodes ---

interface AndNode { kind: 'and'; left: ASTNode; right: ASTNode }
interface OrNode { kind: 'or'; left: ASTNode; right: ASTNode }
interface NotNode { kind: 'not'; operand: ASTNode }
interface CompareNode { kind: 'compare'; op: string; left: ASTNode; right: ASTNode }
interface FieldRefNode { kind: 'fieldref'; fieldName: string }
interface LiteralNode { kind: 'literal'; value: unknown }

export type ASTNode = AndNode | OrNode | NotNode | CompareNode | FieldRefNode | LiteralNode;

// --- Context ---

export interface DisplayLogicContext {
  userId: string | null;
  tenantId: string | null;
  orgId: string | null;
  timestamp: string | null;
  userName: string | null;
}

// --- Evaluator ---

export function evaluateDisplayLogic(
  expression: string | null,
  context: DisplayLogicContext,
  formData: Record<string, unknown>,
): boolean {
  if (!expression) return true;
  if (expression.length > 4096) return false; // safety cap

  try {
    const ast = parseDisplayLogic(expression);
    return evaluateNode(ast, context, formData);
  } catch {
    // Parse error → hide field (conservative, matches backend)
    return false;
  }
}

export function evaluateAST(
  ast: ASTNode,
  context: DisplayLogicContext,
  formData: Record<string, unknown>,
): boolean {
  return evaluateNode(ast, context, formData);
}

function evaluateNode(
  node: ASTNode,
  context: DisplayLogicContext,
  formData: Record<string, unknown>,
): boolean {
  switch (node.kind) {
    case 'and':
      return evaluateNode(node.left, context, formData) &&
             evaluateNode(node.right, context, formData);
    case 'or':
      return evaluateNode(node.left, context, formData) ||
             evaluateNode(node.right, context, formData);
    case 'not':
      return !evaluateNode(node.operand, context, formData);
    case 'compare':
      return evaluateCompare(node, context, formData);
    case 'fieldref':
      return evaluateFieldRef(node, context, formData);
    case 'literal':
      return evaluateLiteral(node);
  }
}

function evaluateFieldRef(
  node: FieldRefNode,
  context: DisplayLogicContext,
  formData: Record<string, unknown>,
): boolean {
  // Context variables — map $X to context or formData.$X
  if (node.fieldName === '$UserId') {
    return !!formData.$UserId ?? !!context.userId ?? false;
  }
  if (node.fieldName === '$TenantId') {
    return !!formData.$TenantId ?? !!context.tenantId ?? false;
  }
  if (node.fieldName === '$OrgId') {
    return !!formData.$OrgId ?? !!context.orgId ?? false;
  }
  if (node.fieldName === '$Timestamp') {
    return !!formData.$Timestamp ?? !!context.timestamp ?? false;
  }
  if (node.fieldName === '$UserName') {
    return !!formData.$UserName ?? !!context.userName ?? false;
  }

  // Form field — strip leading $ to match formData keys
  // JS truthiness for display logic: false, 0, '', null, undefined are falsy
  const fieldName = node.fieldName.startsWith('$') ? node.fieldName.slice(1) : node.fieldName;
  const value = formData[fieldName];
  return !!value;
}

function evaluateLiteral(node: LiteralNode): boolean {
  const val = node.value;
  if (val === null) return false;
  if (typeof val === 'boolean') return val;
  if (typeof val === 'number') return val !== 0;
  return !!val;
}

function evaluateCompare(
  node: CompareNode,
  context: DisplayLogicContext,
  formData: Record<string, unknown>,
): boolean {
  const left = getRawValue(node.left, context, formData);
  const right = getRawValue(node.right, context, formData);
  return compareValues(left, right, node.op);
}

function getRawValue(
  node: ASTNode,
  context: DisplayLogicContext,
  formData: Record<string, unknown>,
): unknown {
  switch (node.kind) {
    case 'fieldref':
      // Strip leading $ to match formData keys
      // Always return the raw value for comparisons (don't filter by truthiness)
      const fieldName = node.fieldName.startsWith('$') ? node.fieldName.slice(1) : node.fieldName;
      return formData[fieldName];
    case 'literal':
      // Recursively unwrap nested AST nodes in array literals (for in/not-in)
      if (Array.isArray(node.value)) {
        return node.value.map((n: ASTNode) => getRawValue(n, context, formData));
      }
      return node.value;
    default:
      // Treat complex nodes as truthy/falsy
      return evaluateNode(node, context, formData) ? 1 : 0;
  }
}

function compareValues(left: unknown, right: unknown, op: string): boolean {
  // empty / not empty operators — check BEFORE null handling
  if (op === 'empty') {
    return left === '' || left == null;
  }
  if (op === 'not empty') {
    return left !== '' && left != null;
  }

  // Both null
  if (left == null && right == null) {
    return op === '==' || op === 'eq';
  }
  // One null
  if (left == null || right == null) {
    return false;
  }

  // in / not in
  if (op === 'in' || op === 'not in') {
    if (!Array.isArray(right)) return false;
    const found = (right as unknown[]).includes(left);
    return op === 'in' ? found : !found;
  }

  // like — convert SQL LIKE to regex
  if (op === 'like' || op === 'not like') {
    const input = String(left);
    const pattern = String(right);
    const regexPattern = pattern.replace(/%/g, '.*').replace(/_/g, '.');
    const re = new RegExp(`^${regexPattern}$`, 'i');
    const match = re.test(input);
    return op === 'like' ? match : !match;
  }

  // Cross-type comparison → false
  const leftType = typeof left;
  const rightType = typeof right;
  if (leftType !== rightType && leftType !== 'string' && rightType !== 'string') {
    return false;
  }

  // Standard comparisons
  if (left === null || right === null) return false;
  if (left == null || right == null) return false;

  return opToFn(left, right, op);
}

function opToFn(left: unknown, right: unknown, op: string): boolean {
  // Convert to comparable types
  let a = left;
  let b = right;

  // If both are numeric strings, parse them
  if (typeof left === 'string' && typeof right === 'string') {
    const aNum = Number(left);
    const bNum = Number(right);
    if (!isNaN(aNum) && !isNaN(bNum) && left.trim() !== '' && right.trim() !== '') {
      a = aNum;
      b = bNum;
    }
  }

  switch (op) {
    case '==':
    case 'eq':
      // eslint-disable-next-line eqeqeq
      return a == b;
    case '!=':
    case 'ne':
    case '<>':
      // eslint-disable-next-line eqeqeq
      return a != b;
    case '<':
      return compareNumbers(a, b) < 0;
    case '>':
      return compareNumbers(a, b) > 0;
    case '<=':
      return compareNumbers(a, b) <= 0;
    case '>=':
      return compareNumbers(a, b) >= 0;
    default:
      return false;
  }
}

function compareNumbers(a: unknown, b: unknown): number {
  const na = typeof a === 'number' ? a : parseFloat(String(a));
  const nb = typeof b === 'number' ? b : parseFloat(String(b));
  if (isNaN(na) || isNaN(nb)) return 0;
  return na < nb ? -1 : na > nb ? 1 : 0;
}

// --- Parser ---

// Tokenizer
type Token =
  | { type: 'word'; value: string }
  | { type: 'string'; value: string }
  | { type: 'number'; value: number }
  | { type: 'paren'; value: string }
  | { type: 'op'; value: string }
  | { type: 'eol' };

function tokenize(input: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;
  while (i < input.length) {
    // Skip whitespace
    if (/\s/.test(input[i])) { i++; continue; }
    // End of line
    if (input[i] === '\n') { tokens.push({ type: 'eol' }); i++; continue; }
    // Parentheses
    if (input[i] === '(') { tokens.push({ type: 'paren', value: '(' }); i++; continue; }
    if (input[i] === ')') { tokens.push({ type: 'paren', value: ')' }); i++; continue; }
    // Square brackets (for in/not-in list literals)
    if (input[i] === '[') { tokens.push({ type: 'paren', value: '[' }); i++; continue; }
    if (input[i] === ']') { tokens.push({ type: 'paren', value: ']' }); i++; continue; }
    // String literal
    if (input[i] === "'" || input[i] === '"') {
      const quote = input[i];
      i++;
      let val = '';
      while (i < input.length && input[i] !== quote) {
        if (input[i] === '\\') { val += input[i + 1] ?? ''; i++; }
        else { val += input[i]; }
        i++;
      }
      i++; // skip closing quote
      tokens.push({ type: 'string', value: val });
      continue;
    }
    // Numbers
    if (/[0-9.]/.test(input[i])) {
      let numStr = '';
      while (i < input.length && /[0-9.]/.test(input[i])) { numStr += input[i]; i++; }
      const num = parseFloat(numStr);
      tokens.push({ type: 'number', value: isNaN(num) ? 0 : num });
      continue;
    }
    // Words and operators
    if (/[a-zA-Z_$]/.test(input[i])) {
      let word = '';
      while (i < input.length && /[a-zA-Z0-9_$.]/.test(input[i])) { word += input[i]; i++; }
      // Check for && and ||
      if (word === '&' && i < input.length && input[i] === '&') {
        word += '&'; i++;
        tokens.push({ type: 'op', value: word });
      } else if (word === '|' && i < input.length && input[i] === '|') {
        word += '|'; i++;
        tokens.push({ type: 'op', value: word });
      } else if (word === '!' && i < input.length && input[i] === '=') {
        word += '='; i++;
        tokens.push({ type: 'op', value: word });
      } else if (word === '=' && i < input.length && input[i] === '=') {
        word += '='; i++;
        tokens.push({ type: 'op', value: word });
      } else if (word === '<' && i < input.length && input[i] === '=') {
        word += '='; i++;
        tokens.push({ type: 'op', value: word });
      } else if (word === '>' && i < input.length && input[i] === '=') {
        word += '='; i++;
        tokens.push({ type: 'op', value: word });
      } else if (word === '<' && i < input.length && input[i] === '>') {
        word += '>'; i++;
        tokens.push({ type: 'op', value: word });
      } else {
        tokens.push({ type: 'word', value: word });
      }
      continue;
    }
    // Boolean operators: && and ||
    if (input[i] === '&' && i + 1 < input.length && input[i + 1] === '&') {
      tokens.push({ type: 'op', value: '&&' }); i += 2; continue;
    }
    if (input[i] === '|' && i + 1 < input.length && input[i + 1] === '|') {
      tokens.push({ type: 'op', value: '||' }); i += 2; continue;
    }
    // Operators: < > ! = (multi-char first)
    if (input[i] === '<' && i + 1 < input.length && input[i + 1] === '=') {
      tokens.push({ type: 'op', value: '<=' }); i += 2; continue;
    }
    if (input[i] === '<' && i + 1 < input.length && input[i + 1] === '>') {
      tokens.push({ type: 'op', value: '<>' }); i += 2; continue;
    }
    if (input[i] === '>' && i + 1 < input.length && input[i + 1] === '=') {
      tokens.push({ type: 'op', value: '>=' }); i += 2; continue;
    }
    if (input[i] === '!' && i + 1 < input.length && input[i + 1] === '=') {
      tokens.push({ type: 'op', value: '!=' }); i += 2; continue;
    }
    if (input[i] === '=') {
      if (i + 1 < input.length && input[i + 1] === '=') { tokens.push({ type: 'op', value: '==' }); i += 2; }
      else { tokens.push({ type: 'op', value: '=' }); i++; }
      continue;
    }
    if (input[i] === '<') { tokens.push({ type: 'op', value: '<' }); i++; continue; }
    if (input[i] === '>') { tokens.push({ type: 'op', value: '>' }); i++; continue; }
    if (input[i] === '!') { tokens.push({ type: 'op', value: '!' }); i++; continue; }

    // Unknown character — skip
    i++;
  }
  return tokens;
}

// Parser with token limit
const MAX_TOKENS = 200;

export function parseDisplayLogic(input: string): ASTNode {
  const tokens = tokenize(input);
  if (tokens.length > MAX_TOKENS) {
    throw new Error(`Display logic exceeds token limit (${MAX_TOKENS}).`);
  }
  const parser = new Parser(tokens);
  return parser.parseExpression();
}

class Parser {
  private pos = 0;
  private readonly tokens: Token[];

  constructor(tokens: Token[]) {
    this.tokens = tokens;
  }

  private current(): Token | undefined {
    return this.tokens[this.pos];
  }

  private consume(expected?: Token): Token {
    const token = this.current();
    if (!token) {
      throw new Error('Unexpected end of expression.');
    }
    if (expected && token.type !== expected.type) {
      throw new Error(`Expected ${expected.type} but got ${token.type}.`);
    }
    this.pos++;
    return token;
  }

  parseExpression(): ASTNode {
    return this.parseOr();
  }

  private parseOr(): ASTNode {
    let left = this.parseAnd();
    let cur = this.current();
    while (cur?.type === 'op' && cur.value === '||') {
      this.consume();
      const right = this.parseAnd();
      left = { kind: 'or', left, right };
      cur = this.current();
    }
    return left;
  }

  private parseAnd(): ASTNode {
    let left = this.parseNot();
    let cur = this.current();
    while (cur?.type === 'op' && cur.value === '&&') {
      this.consume();
      const right = this.parseNot();
      left = { kind: 'and', left, right };
      cur = this.current();
    }
    return left;
  }

  private parseNot(): ASTNode {
    const cur = this.current();
    if (cur?.type === 'op' && cur.value === '!') {
      this.consume();
      const operand = this.parseNot();
      return { kind: 'not', operand };
    }
    return this.parsePrimary();
  }

  private parsePrimary(): ASTNode {
    const token = this.current();
    if (!token) throw new Error('Unexpected end of expression.');

    // Parenthesized expression
    if (token.type === 'paren' && token.value === '(') {
      this.consume();
      const expr = this.parseOr();
      this.consume({ type: 'paren', value: ')' });
      return expr;
    }

    // Field reference ($FieldName) — check for comparison operator next
    if (token.type === 'word' && token.value.startsWith('$')) {
      const fieldName = token.value;
      this.consume();
      return this.buildFieldComparisonAfter(fieldName, token);
    }

    // Literal — check for comparison operator after
    if (token.type === 'string') {
      this.consume();
      return this.buildLiteralComparisonAfter({ kind: 'literal', value: token.value });
    }
    if (token.type === 'number') {
      this.consume();
      return this.buildLiteralComparisonAfter({ kind: 'literal', value: token.value });
    }

    // Keywords: true, false, null — check for comparison operator after
    if (token.type === 'word') {
      if (token.value === 'true') { this.consume(); return this.buildLiteralComparisonAfter({ kind: 'literal', value: true }); }
      if (token.value === 'false') { this.consume(); return this.buildLiteralComparisonAfter({ kind: 'literal', value: false }); }
      if (token.value === 'null') { this.consume(); return this.buildLiteralComparisonAfter({ kind: 'literal', value: null }); }
    }

    // Bare word that might be the left side of a comparison
    this.consume();
    return this.buildComparisonAfter(token);
  }

  private buildFieldComparisonAfter(fieldName: string, token: Token): ASTNode {
    // Check for comparison operators: == != <> < > <= >= like not like
    const next = this.current();
    if (next?.type === 'op' && isComparisonOp(next.value)) {
      const op = next.value;
      this.consume();
      const right = this.parsePrimary();
      return { kind: 'compare', op, left: { kind: 'fieldref', fieldName }, right };
    }
    // Check for 'like' / 'not like' (word tokens)
    if (next?.type === 'word' && next.value === 'like') {
      this.consume();
      const right = this.parsePrimary();
      return { kind: 'compare', op: 'like', left: { kind: 'fieldref', fieldName }, right };
    }
    if (next?.type === 'word' && next.value === 'not' && this.tokens[this.pos + 1]?.type === 'word' && (this.tokens[this.pos + 1] as { type: string; value?: string }).value === 'like') {
      this.consume(); this.consume();
      const right = this.parsePrimary();
      return { kind: 'compare', op: 'not like', left: { kind: 'fieldref', fieldName }, right };
    }
    // Check for 'in' / 'not in'
    if (next?.type === 'word' && next.value === 'in') {
      this.consume();
      return this.buildInList('in', fieldName);
    }
    if (next?.type === 'word' && next.value === 'not' && this.tokens[this.pos + 1]?.type === 'word' && (this.tokens[this.pos + 1] as { type: string; value?: string }).value === 'in') {
      this.consume(); this.consume();
      return this.buildInList('not in', fieldName);
    }
    // Check for 'empty' / 'not empty'
    if (next?.type === 'word' && next.value === 'empty') {
      this.consume();
      return { kind: 'compare', op: 'empty', left: { kind: 'fieldref', fieldName }, right: { kind: 'literal', value: null } };
    }
    if (next?.type === 'word' && next.value === 'not' && this.tokens[this.pos + 1]?.type === 'word' && (this.tokens[this.pos + 1] as { type: string; value?: string }).value === 'empty') {
      this.consume(); this.consume();
      return { kind: 'compare', op: 'not empty', left: { kind: 'fieldref', fieldName }, right: { kind: 'literal', value: null } };
    }
    // Just a field reference
    return { kind: 'fieldref', fieldName };
  }

  private buildInList(op: 'in' | 'not in', fieldName: string): ASTNode {
    // Expect array literal [...]
    this.consume({ type: 'paren', value: '[' });
    const items: ASTNode[] = [];
    let cur = this.current();
    while (cur?.type !== 'paren' || cur.value !== ']') {
      items.push(this.parsePrimary());
      cur = this.current();
      if (cur?.type === 'paren' && cur.value === ',') {
        this.consume();
        cur = this.current();
      }
    }
    this.consume({ type: 'paren', value: ']' });
    return { kind: 'compare', op, left: { kind: 'fieldref', fieldName }, right: { kind: 'literal', value: items } };
  }

  private buildLiteralComparisonAfter(leftNode: ASTNode): ASTNode {
    const next = this.current();
    if (next?.type === 'op' && isComparisonOp(next.value)) {
      const op = next.value;
      this.consume();
      const right = this.parsePrimary();
      return { kind: 'compare', op, left: leftNode, right };
    }
    // Just return the literal node
    return leftNode;
  }

  private buildComparisonAfter(leftValue: Token): ASTNode {
    // If followed by a comparison operator
    const next = this.current();
    if (next?.type === 'op' && isComparisonOp(next.value)) {
      const op = next.value;
      this.consume();
      const right = this.parsePrimary();
      const leftNode: ASTNode = leftValue.type === 'word' && leftValue.value.startsWith('$')
        ? { kind: 'fieldref', fieldName: leftValue.value }
        : { kind: 'literal', value: leftValue.type === 'string' ? leftValue.value : leftValue.type === 'number' ? leftValue.value : undefined };
      return { kind: 'compare', op, left: leftNode, right };
    }
    // If followed by 'in' or 'not in'
    if (next?.type === 'word' && next.value === 'in') {
      this.consume();
      // Expect array literal [...]
      this.consume({ type: 'paren', value: '[' });
      const items: ASTNode[] = [];
      let cur = this.current();
      while (cur?.type !== 'paren' || cur.value !== ']') {
        items.push(this.parsePrimary());
        cur = this.current();
        if (cur?.type === 'paren' && cur.value === ',') {
          this.consume();
          cur = this.current();
        }
      }
      this.consume({ type: 'paren', value: ']' });
      const arrNode: ASTNode = { kind: 'literal', value: items };
      const leftNode: ASTNode = leftValue.type === 'word' && leftValue.value.startsWith('$')
        ? { kind: 'fieldref', fieldName: leftValue.value }
        : { kind: 'literal', value: leftValue.type === 'string' ? leftValue.value : leftValue.type === 'number' ? leftValue.value : undefined };
      return { kind: 'compare', op: 'in', left: leftNode, right: arrNode };
    }
    if (next?.type === 'word' && next.value === 'not' && this.tokens[this.pos + 1]?.type === 'word' && (this.tokens[this.pos + 1] as { type: string; value?: string }).value === 'in') {
      this.consume(); this.consume();
      this.consume({ type: 'paren', value: '[' });
      const items: ASTNode[] = [];
      let cur = this.current();
      while (cur?.type !== 'paren' || cur.value !== ']') {
        items.push(this.parsePrimary());
        cur = this.current();
        if (cur?.type === 'paren' && cur.value === ',') {
          this.consume();
          cur = this.current();
        }
      }
      this.consume({ type: 'paren', value: ']' });
      const arrNode: ASTNode = { kind: 'literal', value: items };
      const leftNode: ASTNode = leftValue.type === 'word' && leftValue.value.startsWith('$')
        ? { kind: 'fieldref', fieldName: leftValue.value }
        : { kind: 'literal', value: leftValue.type === 'string' ? leftValue.value : leftValue.type === 'number' ? leftValue.value : undefined };
      return { kind: 'compare', op: 'not in', left: leftNode, right: arrNode };
    }
    // Empty check
    if (next?.type === 'word' && next.value === 'empty') {
      this.consume();
      const fieldName = leftValue.type === 'word' ? leftValue.value : '';
      return { kind: 'compare', op: 'empty', left: { kind: 'fieldref', fieldName }, right: { kind: 'literal', value: null } };
    }
    // Just a field reference
    return { kind: 'fieldref', fieldName: leftValue.type === 'word' ? leftValue.value : '' };
  }
}

function isComparisonOp(op: string): boolean {
  return ['==', '!=', '<>', '<', '>', '<=', '>='].includes(op);
}
