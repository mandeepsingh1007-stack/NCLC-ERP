# ADR-0005: React Component Library

- **ID**: ADR-0005
- **Status**: Proposed
- **Date**: 2026-08-15
- **Context**: Phase 3 requires a React component library for metadata-driven UI (generic forms, grids, lookups, menus). The frontend has React 19 + TypeScript, react-hook-form, TanStack Query, and axios. No UI component library is installed. The HLD/LLD specifies enterprise dictionary-driven UI, not consumer-facing apps.

## Problem

How do we select a React component library that supports:
- Enterprise dictionary-driven UI (forms, grids, lookups, menus, trees)
- Metadata-driven rendering (generic components, not hand-built per screen)
- Accessibility (WCAG 2.1 AA)
- Responsive behavior
- Long-term maintainability
- Small enough bundle size for a SPA
- Strong TypeScript support
- Customizable theming via ConfigProvider

## Decision

We use **Ant Design (antd) v5.x** with `@ant-design/cssinjs` ConfigProvider for theming.

## Alternatives Considered

### Ant Design v5.x (CHOSEN)
- **Pros**: 100+ enterprise components, built-in forms/tables/trees/modals/menus/pickers, virtualized Table for 10K+ rows, ConfigProvider theming, i18n built-in, WCAG 2.1 AA, strong TypeScript, tree-shakeable, actively maintained (Alibaba), proven in enterprise dictionary systems
- **Cons**: ~150KB gzipped base bundle, some components have opinionated styling

### Material UI (MUI) v5
- **Pros**: Excellent DX, Google-backed, great documentation, strong community, good theming
- **Cons**: More consumer-facing aesthetic, Table component NOT virtualized by default (requires paid `@mui/x-data-grid`), less comprehensive form component set than AntD, smaller component count overall

### Radix UI + Custom Components
- **Pros**: Unstyled headless primitives, maximum visual control, small bundle per component
- **Cons**: Requires building EVERY component from scratch (forms, tables, modals, menus, pickers, trees, drawers) — enormous effort, defeats the purpose of a metadata platform that must prove generic UI works quickly, no built-in accessibility on primitives (requires manual composition)

### Unstyled / Custom Build
- **Pros**: Zero dependencies
- **Cons**: Unacceptable build effort for a platform whose value proposition is rapid generic UI generation

## Decision Rationale

Ant Design wins for this specific use case because:

1. **Comprehensive component set**: Forms (Form + Form.Item + rule-based validation), Tables (virtualized, sortable, filterable, paginated), Modals/Drawers (for lookups), Menus (hierarchical), Pickers (date, time, color), Trees (for SysTree), Tabs, Steps, Cascaders, Selects — all available out of the box.

2. **Enterprise alignment**: Ant Design was built for enterprise applications with complex data entry and management — exactly what a metadata-driven platform delivers. The visual style matches the "dictionary-first" enterprise aesthetic.

3. **ConfigProvider theming**: Runtime theme customization via `ConfigProvider.theme` — essential for a platform where tenants may want different themes but the component library is shared.

4. **Virtualized Table**: Handles 10K+ rows with virtual scrolling — critical for high-volume data grids without requiring a separate paid library.

5. **Form integration**: Form.Item provides rule-based validation, layout (col/row span), and error display — maps directly to SysField metadata (isMandatory, colSpan, displayLogic).

6. **Tree-shakeable**: Only imported components add to bundle. `import { Form } from 'antd'` adds only Form, not the entire library.

## Security Implications

- **Third-party dependency**: Ant Design is a well-maintained, widely-used library (100K+ stars, 50M+ weekly npm downloads). Supply chain risk is low but must be monitored via Dependabot/Snyk.
- **XSS via component props**: Ant Design components auto-escape text content. `dangerouslySetInnerHTML` is only used by the `dangerouslySetInnerHTML` prop — which we will NOT use for metadata-driven content.
- **CSP compatibility**: Ant Design uses inline styles via CSS-in-JS (cssinjs), which requires `script-src 'self'` in CSP — acceptable for a SPA.
- **No eval() or dynamic imports**: Ant Design does not use eval() or Function() in its runtime.

## Performance Implications

- **Bundle size**: Base antd ~150KB gzipped. With tree-shaking and only commonly-used components (Form, Input, Table, Select, Modal, Menu, Tabs, Button, Space, Typography, message, notification, DatePicker, Upload, Switch, InputNumber), estimated total ~250KB gzipped.
- **Tree-shaking**: Webpack/Vite tree-shaking works with antd's ESM exports.
- **Virtual scrolling**: Table component uses virtual scroll by default when `scroll.y` is set — renders only visible rows.
- **Lazy loading**: Heavy components (Modal, DatePicker, Tree) can be lazy-loaded via React.lazy().
- **Code splitting**: Ant components can be code-split per route.

## UX Implications

- **Enterprise look-and-feel**: Consistent, professional appearance out of the box.
- **Form validation UX**: Built-in error display, success/warning/info states.
- **Loading states**: Spin component + loading props on major components.
- **Empty states**: Empty component for placeholder content.
- **Notifications**: message + notification for user feedback.
- **Icons**: @ant-design/icons — 3000+ icons, tree-shakeable.

## Backward Compatibility

- **React 19 required**: Ant Design v5 uses React.Context extensively, requires React 16.9+. Already using React 19 — fully compatible.
- **CSS-in-JS**: Ant Design v5 uses `@ant-design/cssinjs` instead of Less compilation. No CSS build changes needed.
- **No breaking changes**: Migration from v4 to v5 is straightforward (mostly deprecations).

## Testing Implications

- **Testing Library**: `@testing-library/react` works with Ant Design. Use `screen.getByRole('textbox')` for inputs, `screen.getByRole('combobox')` for selects.
- **Mocking**: Ant components can be mocked with `jest.mock('antd')` if needed.
- **Snapshot testing**: Ant Design components produce stable snapshots (avoid snapshot testing for layout — use functional assertions).
- **Accessibility testing**: `jest-axe` for automated a11y testing of Ant components (they are WCAG 2.1 AA compliant).

## Migration Implications

- **Greenfield**: No migration path needed. React codebase is empty for Phase 3.
- **Incremental adoption**: Ant Design can be added alongside custom components.
- **No legacy components to replace**: No existing React components exist yet.

## Future Extensibility

- **ConfigProvider tokens**: Runtime customization of colors, typography, spacing, border radius, breakpoints.
- **Slots**: Ant Design v5 component slots allow customizing internal component rendering.
- **Custom themes**: Full theme token override for multi-tenant theming.
- **Component composition**: Ant components compose well — Form.Item + custom renderers for metadata-driven forms.

## Consequences

### Because of this decision:

**Pros:**
- Rapid development of generic forms/grids/lookups/menus
- Enterprise-grade accessibility built in
- Virtualized tables for high-volume data
- ConfigProvider enables multi-tenant theming
- 100+ components reduce custom code
- Strong TypeScript support
- Large community and active maintenance

**Cons:**
- ~250KB gzipped bundle for common components (acceptable for enterprise SPA)
- Ant Design visual style may not match all brand guidelines (mitigated by ConfigProvider theming)
- Some components are opinionated and hard to customize deeply (rarely needed for metadata-driven UI where layout is defined by metadata, not component props)

## Design Tokens

| Token | Value | Purpose |
|---|---|---|
| colorPrimary | `#1677FF` | Default Ant Design blue — actions, links |
| colorSuccess | `#52C41A` | Success states |
| colorWarning | `#FAAD14` | Warning states |
| colorError | `#FF4D4F` | Error states, mandatory fields |
| fontFamily | System font stack | Platform-native feel |
| fontSize | 14px base | Body text |
| borderRadius | 6px | Cards, inputs, dialogs |
| paddingXXS | 2px | Tight spacing |
| paddingXS | 4px | Tight spacing |
| paddingSM | 8px | Small spacing |
| padding | 16px | Base spacing unit |
| screenMD | 768px | Tablet breakpoint |
| screenLG | 1024px | Desktop breakpoint |

## References

- Ant Design documentation: https://ant.design
- Ant Design GitHub: https://github.com/ant-design/ant-design
- ConfigProvider theming: https://ant.design/docs/react/customize-theme
- WCAG 2.1 AA: https://www.w3.org/WAI/WCAG21/quickref/
