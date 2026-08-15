# Phase 4 — React Runtime: Design Document

**Phase:** 4 — React Runtime
**Date:** 2026-08-15
**Status:** Design
**Author:** Claude (agentic orchestrator)
**Authority:** `docs/architecture/FINAL-MASTER-HLD-LLD-v2.md` Section 32

---

## 1. Overview

Phase 4 connects the backend metadata API (Phase 3) to a React frontend that renders generic forms, grids, lookups, and menus — entirely driven by metadata. No module-specific React screens.

### Scope

| In Scope | Out of Scope |
|---|---|
| TypeScript contracts from C# API contracts | Phase 5: Authentication (JWT) |
| Ant Design v5.x integration | Phase 5: Tenant isolation enforcement |
| Dynamic form rendering from SysWindow/SysTab/SysField | Phase 6: Workflow |
| Dynamic data grid with pagination/sort/filter | Phase 7: Platform services |
| Lookup field rendering (LIST/TABLE/SEARCH) | Phase 7: Audit/attachments |
| Menu navigation from SysMenu | Phase 10: First reference module |
| Display logic evaluation in-browser | |
| Field group sections | |
| Loading/error/empty states | |
| Accessibility (WCAG 2.1 AA) | |

### Architecture Principle

```
Metadata (PostgreSQL)
    ↓
MetadataGraph (Phase 2)
    ↓
Meta API / Data API / Lookup API (Phase 3)
    ↓
React Runtime (Phase 4) ← THIS PHASE
```

The frontend is a pure consumer of the Phase 3 API. No backend changes required beyond what Phase 3 already provides.

---

## 2. Frontend Architecture

### 2.1 Technology Stack

| Layer | Technology | Rationale |
|---|---|---|
| Framework | React 19 + TypeScript | Already in `frontend/package.json` |
| Component Library | Ant Design v5.x | ADR-0005 selected |
| Forms | react-hook-form 7 | Already in `frontend/package.json` |
| Data Fetching | TanStack Query 5 | Already in `frontend/package.json` |
| State Management | React Context + TanStack Query | No Redux needed |
| Routing | React Router 6 | Standard SPA routing |
| Icons | @ant-design/icons | 3000+ icons, tree-shakeable |
| Testing | @testing-library/react + vitest | Fast, compatible with AntD |

### 2.2 Directory Structure

```
frontend/src/
├── api/
│   ├── client.ts
│   ├── contracts/
│   │   ├── window.ts
│   │   ├── data.ts
│   │   ├── lookup.ts
│   │   └── menu.ts
│   ├── metaApi.ts
│   ├── dataApi.ts
│   └── lookupApi.ts
├── components/
│   ├── DynamicForm.tsx
│   ├── DynamicGrid.tsx
│   ├── LookupField.tsx
│   ├── FieldGroup.tsx
│   ├── DisplayLogicToggle.tsx
│   ├── MenuNavigation.tsx
│   ├── LoadingState.tsx
│   ├── EmptyState.tsx
│   └── ErrorState.tsx
├── hooks/
│   ├── useDisplayLogic.ts
│   ├── useFieldValidation.ts
│   └── useMenuTree.ts
├── pages/
│   ├── MainWindow.tsx
│   └── MainGrid.tsx
├── utils/
│   ├── controlTypeMap.ts
│   ├── displayLogicEval.ts
│   └── fieldHelpers.ts
├── App.tsx
└── index.tsx
```

### 2.3 API Client Layer

```typescript
// api/client.ts
import axios from 'axios';

const api = axios.create({
  baseURL: '/api',
  timeout: 30000,
});

// Phase 4: Auth interceptor placeholder (Phase 5 will add JWT)
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;
```

### 2.4 TypeScript Contracts

These mirror the C# API response shapes from Phase 3 contracts:

```typescript
// api/contracts/window.ts
export interface WindowContract {
  windowId: number;
  columnName: string;
  name: string;
  description?: string;
  tabs: TabContract[];
}

export interface TabContract {
  tabId: number;
  columnName: string;
  name: string;
  table: string;
  isGrid: boolean;
  fields: FieldContract[];
  fieldGroups: FieldGroupContract[];
}

export interface FieldContract {
  columnName: string;
  label: string;
  help?: string;
  controlType: ControlType;
  isMandatory: boolean;
  isReadOnly: boolean;
  isMandatoryOverride: boolean;
  isReadOnlyOverride: boolean;
  colSpan: number;
  rowSpan: number;
  defaultValue?: string;
  displayLogic?: string;
  readOnlyLogic?: string;
  mandatoryLogic?: string;
  fieldGroup?: string;
  sysReference?: ReferenceInfo;
  fieldLength?: number;
}

export type ControlType =
  | 'TextInput'
  | 'TextArea'
  | 'NumberInput'
  | 'DatePicker'
  | 'YesNoToggle'
  | 'Lookup'
  | 'MultiSelect'
  | 'RichText'
  | 'Image'
  | 'FileUpload'
  | 'Date'
  | 'Time'
  | 'DateTime'
  | 'Email'
  | 'URL'
  | 'Password';

export interface ReferenceInfo {
  name: string;
  validationType: 'baseType' | 'list' | 'table' | 'search';
  valueFormat?: string;
}

export interface FieldGroupContract {
  groupName: string;
  label: string;
  colSpan: number;
  isCollapsed: boolean;
  fields: string[];
}
```

### 2.5 Data Contracts

```typescript
// api/contracts/data.ts
export interface DataTableResponse<T = Record<string, unknown>> {
  items: T[];
  pagination: PaginationInfo;
}

export interface PaginationInfo {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}
```

---

## 3. Component Design

### 3.1 DynamicForm — Core Component

The DynamicForm renders a form entirely from `FieldContract[]` metadata.

```typescript
// components/DynamicForm.tsx
interface DynamicFormProps {
  windowId: number;
  tabId?: number;
  initialData?: Record<string, unknown>;
  mode: 'create' | 'edit' | 'view';
  onSubmit: (data: Record<string, unknown>) => Promise<void>;
}

/**
 * Renders a React Hook Form + Ant Design Form from metadata.
 *
 * Mapping logic:
 * 1. Fetch window metadata from GET /api/meta/window/{windowId}
 * 2. Select the appropriate tab (default if not specified)
 * 3. Group fields by SysFieldGroup
 * 4. For each field:
 *    a. Evaluate displayLogic → skip render if false
 *    b. Evaluate readOnlyLogic → set Form.Item.disabled
 *    c. Evaluate mandatoryLogic → set Form.Item.rules.required
 *    d. Map controlType → Ant Design input component
 *    e. Apply field-level validation (type, length, reference)
 *    f. Apply colSpan/rowSpan via Row/Col grid
 */
```

**ControlType mapping:**

| ControlType | Ant Design Component | react-hook-form |
|---|---|---|
| TextInput | `<Input />` | `register(name, { required, maxLength })` |
| TextArea | `<Input.TextArea />` | `register(name, { required, maxLength })` |
| NumberInput | `<InputNumber />` | `register(name, { valueAsNumber })` |
| DatePicker | `<DatePicker />` | `register(name)` |
| YesNoToggle | `<Switch />` | `register(name)` |
| Lookup | `<Select />` or `<AutoComplete />` | `register(name)` |
| MultiSelect | `<Select mode="multiple" />` | `register(name)` |
| Email | `<Input type="email" />` | `register(name, { pattern: emailRegex })` |
| URL | `<Input type="url" />` | `register(name, { pattern: urlRegex })` |
| Password | `<Input.Password />` | `register(name)` |

**Field group rendering:**

```tsx
<ProForm>
  {fieldGroups.map(group => (
    <ProFormCollapse key={group.groupName} {...group.isCollapsed ? { defaultActiveKey: [] } : {}}}>
      <ProForm.Item name={group.groupName}>
        {group.fields.map(fieldName => renderField(fieldName))}
      </ProForm.Item>
    </ProFormCollapse>
  ))}
  {/* Fields not in any group render at top level */}
</ProForm>
```

### 3.2 LookupField — Reference Field Renderer

```typescript
// components/LookupField.tsx
interface LookupFieldProps {
  columnName: string;
  reference: ReferenceInfo;
  value?: string;
  onChange?: (value: string) => void;
  search?: boolean;
}

/**
 * For reference fields, renders either:
 * - <Select> for LIST references
 * - <AutoComplete> for TABLE references (search API)
 * - <AutoComplete> for SEARCH references (whereClause search)
 *
 * Caching: TanStack Query cache (5 min TTL matching backend)
 */
```

### 3.3 DynamicGrid — Data Grid

```typescript
// components/DynamicGrid.tsx
interface DynamicGridProps {
  tableName: string;
  windowId: number;
  columns?: string[];
  filter?: FilterDSL;
  sortable?: boolean;
  selectable?: boolean;
}

/**
 * Renders an Ant Design Table from data API.
 *
 * Features:
 * 1. Columns from SysField metadata
 * 2. Filter DSL → API query params
 * 3. Pagination via query params
 * 4. Sorting via sortBy/sortDir
 * 5. Virtual scrolling for high-volume
 * 6. Actions: View, Edit, Delete
 */
```

### 3.4 Display Logic Evaluation (Client-Side)

TypeScript port of backend `DisplayLogicEvaluator` (ADR-0006):

```typescript
// utils/displayLogicEval.ts

/**
 * Evaluates display logic expressions in the browser.
 * Same grammar as backend. No eval(). Recursive descent parser → AST → evaluate.
 * Same depth limit (20), token limit (200).
 */
function evaluateDisplayLogic(
  expression: string | null,
  context: DisplayLogicContext,
  formData: Record<string, unknown>,
): boolean {
  if (!expression) return true;
  const ast = parseDisplayLogic(expression);
  return evaluateAST(ast, context, formData);
}

interface DisplayLogicContext {
  userId: string | null;
  tenantId: string | null;
  orgId: string | null;
  timestamp: string | null;
  userName: string | null;
}
```

### 3.5 Menu Navigation

```typescript
// hooks/useMenuTree.ts

/**
 * Transforms flat menu list from GET /api/meta/menu into
 * a hierarchical tree for Ant Design Menu component.
 * Uses ParentId to build tree, sorts by Sequence.
 * Cache: TanStack Query (5 min TTL).
 */
```

---

## 4. State Management

### 4.1 TanStack Query Structure

```typescript
// Window metadata — permanent cache
const useWindow = (windowId: number) =>
  useQuery({
    queryKey: ['meta', 'window', windowId],
    queryFn: () => api.get(`/meta/window/${windowId}`),
    staleTime: Infinity,
  });

// Windows list — short TTL
const useWindows = () =>
  useQuery({
    queryKey: ['meta', 'windows'],
    queryFn: () => api.get('/meta/windows'),
    staleTime: 5 * 60 * 1000,
  });

// Data — cache per query params
const useDataTable = (table: string, params: DataQueryParams) =>
  useQuery({
    queryKey: ['data', table, params],
    queryFn: () => api.get(`/data/${table}`, { params }),
    staleTime: 0,
  });

// Lookup — 5 min TTL
const useLookup = (referenceId: number, search?: string) =>
  useQuery({
    queryKey: ['lookup', referenceId, search],
    queryFn: () => api.get(`/lookup/${referenceId}`, { params: { search } }),
    staleTime: 5 * 60 * 1000,
  });
```

### 4.2 React Context (Global)

```typescript
// context/AppContext.tsx
interface AppContextValue {
  theme: ThemeConfig;
  tenantId: string | null;
  orgId: string | null;
  userId: string | null;
  language: 'en' | 'hi' | 'ta';
}
```

---

## 5. Routing

```typescript
// App.tsx
<Routes>
  <Route path="/window/:windowId" element={<MainWindow />} />
  <Route path="/grid/:windowId" element={<MainGrid />} />
  <Route path="/" element={<Navigate to="/window/1" replace />} />
</Routes>
```

---

## 6. Testing Strategy

### 6.1 Unit Tests

| Component | Tests |
|---|---|
| `displayLogicEval.ts` | AST nodes, null handling, depth limit, token limit, type coercion (126 tests) |
| `controlTypeMap.ts` | All 17 ControlType values map correctly |
| `useDisplayLogic.ts` | Expressions match backend results |
| `useMenuTree.ts` | Flat → tree transformation, sorting |

### 6.2 Component Tests

| Component | Tests |
|---|---|
| `DynamicForm` | Renders from metadata, field groups collapse, displayLogic hides, validation errors display |
| `DynamicGrid` | Columns, pagination, sorting, filter |
| `LookupField` | LIST → Select, TABLE → AutoComplete, search filters |
| `MenuNavigation` | Tree render, click navigates, separators |

---

## 7. Security Considerations

| Concern | Mitigation |
|---|---|
| XSS via metadata content | Ant Design auto-escapes. No `dangerouslySetInnerHTML`. |
| Display logic injection | Same recursive descent parser as backend — no eval(). ADR-0006. |
| Client-side logic bypass | Client-side display logic is UX-only. Server evaluates same logic (Phase 5). |
| Large bundle size | Tree-shaking, code splitting, lazy-loading. |
| Field enumeration | Metadata API gated by Phase 5 access control. |

---

## 8. Implementation Plan

### Sprint 1: Foundation

| Step | Task | Files |
|---|---|---|
| 1 | Install Ant Design + icons | `frontend/package.json` |
| 2 | TypeScript contracts | `api/contracts/*.ts` (4 files) |
| 3 | API client + hooks | `api/client.ts`, `metaApi.ts`, `dataApi.ts`, `lookupApi.ts` |
| 4 | Routing + layout | `App.tsx`, `index.tsx` |
| 5 | Theme configuration | `index.tsx` (ConfigProvider) |

### Sprint 2: Forms

| Step | Task | Files |
|---|---|---|
| 6 | ControlType mapping | `utils/controlTypeMap.ts` |
| 7 | DynamicField renderer | `components/DynamicField.tsx` |
| 8 | FieldGroup section wrapper | `components/FieldGroup.tsx` |
| 9 | Display logic eval (TS) | `utils/displayLogicEval.ts`, `hooks/useDisplayLogic.ts` |
| 10 | DynamicForm component | `components/DynamicForm.tsx` |
| 11 | LookupField component | `components/LookupField.tsx` |

### Sprint 3: Grid + Menu + States

| Step | Task | Files |
|---|---|---|
| 12 | DynamicGrid component | `components/DynamicGrid.tsx` |
| 13 | Menu tree hook + component | `hooks/useMenuTree.ts`, `components/MenuNavigation.tsx` |
| 14 | Loading/Empty/Error states | `components/LoadingState.tsx`, `EmptyState.tsx`, `ErrorState.tsx` |
| 15 | MainWindow page | `pages/MainWindow.tsx` |
| 16 | MainGrid page | `pages/MainGrid.tsx` |

### Sprint 4: Testing

| Step | Task |
|---|---|
| 17 | Unit tests for displayLogicEval, controlTypeMap, useMenuTree |
| 18 | Component tests for DynamicForm, DynamicGrid, LookupField |
| 19 | Contract tests (TypeScript shapes match JSON API) |
| 20 | Build verification + bundle size check |

---

## 9. Warnings & Technical Debt

1. **No auth yet** — All API calls anonymous in Phase 4. Intentional; auth is Phase 5.
2. **POST/PUT/DELETE not yet functional** — Data API mutations return 501. UI submit buttons will fail. Documented — UI prepared, backend deferred.
3. **No tenant isolation in frontend** — Frontend doesn't know about tenant/org yet. Injected from auth tokens in Phase 5.
4. **No i18n yet** — Labels from SysField.Label. Translation is Phase 7.
