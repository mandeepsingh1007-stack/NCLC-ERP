# Phase 3 — UX/UI Design Closure

**Phase:** 3 — UI (Generic Forms, Grids, Lookups, Menus)
**Date:** 2026-08-15
**Status:** Design Closure

---

## 1. Window UX

### 1.1 Layout

```
┌─────────────────────────────────────────────────────┐
│  Navigation Bar                                     │
│  [Menu][User][Notifications]                        │
├─────────────────────────────────────────────────────┤
│  Window Title                    [Actions...]       │
│  Breadcrumb: Home > Books > Library Book            │
├─────────────────────────────────────────────────────┤
│  │ Main │ Details │ Grid │ (Tab Bar)               │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌─ Main Info ─────────────────────────────┐        │
│  │ Field 1    Field 2    Field 3           │        │
│  │ Field 4    Field 5                     │        │
│  └─────────────────────────────────────────┘        │
│                                                     │
│  ┌─ Address ───────────────────────────────┐       │
│  │ Street                              │   │       │
│  │ City         Zip      State         │   │       │
│  └─────────────────────────────────────────┘       │
│                                                     │
├─────────────────────────────────────────────────────┤
│  [Cancel]  [Save]  [Delete]                         │
└─────────────────────────────────────────────────────┘
```

- **Title**: From SysWindow.Name
- **Breadcrumb**: Home > Menu Item > Window Name
- **Action bar**: Context-dependent actions (Save, Delete, custom from SysProcess)
- **Tabs**: From SysTab, ordered by SeqNo
- **Content**: Fields from SysField, grouped by SysFieldGroup
- **Footer**: Standard action buttons

### 1.2 States

| State | Description | Visual |
|---|---|---|
| Loading | Metadata and/or data loading | Spinner in content area |
| Empty (no window) | Window ID not found | 404 page, "Window not found" |
| Empty (no tabs) | Window exists but has no tabs | "No tabs configured" message |
| Empty (no fields) | Tab has no fields | "No fields configured" message |
| Empty (no data) | Create mode or no record | Blank form, ready for input |
| Normal | Record loaded, ready for edit | Form populated with data |
| Dirty | Changes made but not saved | " unsaved changes " indicator |
| Error | API error or validation failure | Error banner at top |

---

## 2. Tab UX

### 2.1 Tab Bar

- Horizontal tab bar below window title
- Active tab highlighted
- Default tab selected on load (from SysTab.IsDefaultTab)
- Too many tabs → horizontal scroll or tab overflow dropdown

```
┌──────────────────────────────────────────────┐
│ [Main] [Details] [History] ─── >             │
└──────────────────────────────────────────────┘
```

### 2.2 Tab States

| State | Description |
|---|---|
| Active | Tab is selected, content visible |
| Inactive | Tab not selected, content hidden |
| Disabled | Tab IsActive=false, not shown in UI |
| Grid mode | Tab with IsGrid=true shows grid instead of form |

---

## 3. Field Rendering

### 3.1 Control Types

| SysReference | ControlType | React Component | Visual |
|---|---|---|---|
| vChar | TextInput | `<Input />` | Single-line text input |
| integer/bigint | NumberInput | `<InputNumber />` | Numeric input with steps |
| date | DateInput | `<DatePicker />` | Date picker dropdown |
| dateTime | DateInput | `<DatePicker showTime />` | Date + time picker |
| yesNo | YesNoToggle | `<Switch />` | Toggle switch |
| LIST | ListDropdown | `<Select />` | Dropdown with static options |
| TABLE | TableLookup | `<TableLookup />` | Button opens lookup dialog |
| SEARCH | SearchPopup | `<SearchPopup />` | Debounced search input |
| text | TextArea | `<TextArea />` | Multi-line text input |

### 3.2 Field States

| State | Description | Visual |
|---|---|---|
| Normal | Ready for input | Standard input styling |
| Mandatory | IsMandatory=true | Red asterisk (*) after label |
| Read-only | IsReadOnly=true or override | Disabled input, value visible |
| Filled | Has a value | Value displayed |
| Empty | No value | Placeholder or empty |
| Error | Validation failed | Red border, error message below |
| Disabled | IsReadOnlyOverride=true | Grayed out, not focusable |
| Hidden | DisplayLogic=false | Not rendered in DOM |
| Loading | Lookup data fetching | Spinner in input |

### 3.3 Field Layout

- Default: 1 field per row, full width (colSpan=12 in a 12-column grid)
- colSpan=6: Two fields side by side
- colSpan=4: Three fields side by side
- Same-line fields (colSpan > 1): Flex layout with equal spacing

```
┌──────────────────────────────────────────┐
│ Title (required) [______________]        │
│                                          │
│ ISBN            Available                │
│ [______________]    [✓]                  │
│                                          │
│ ┌─ Address Info ──────────────────────┐  │
│ │ Street                              │ │
│ │ [_________________________________] │ │
│ │ City            Zip      State      │ │
│ │ [__________]  [____]    [____]     │ │
│ └─────────────────────────────────────┘  │
│                                          │
│ Notes                                │   │
│ [_________________________________]   │   │
│ [_________________________________]   │   │
│ [_________________________________]   │   │
└──────────────────────────────────────────┘
```

---

## 4. Field Grouping

### 4.1 Group Visual

```
┌─ Personal Info              ┐
│ ▼  Personal Info           │  Collapsed
│                            │
│ Name       [______]        │
│ Email      [______]        │
└────────────────────────────┘

┌─ Personal Info              ┐
│ ▲  Personal Info           │  Expanded
│                            │
│ Name       [______]        │
│ Email      [______]        │
└────────────────────────────┘
```

- Collapsible sections (click header to toggle)
- Default collapsed state from SysFieldGroup.IsCollapsed
- Group label from SysElement
- Optional icon prefix
- Visual separator line below group

### 4.2 Group States

| State | Description |
|---|---|---|
| Expanded | Group content visible |
| Collapsed | Group content hidden, header visible |
| Empty | Group exists but has no fields |
| Disabled | Group IsActive=false, not rendered |

---

## 5. Dynamic Visibility (Display Logic)

### 5.1 Behavior

- Fields with `displayLogic` evaluate the expression against current form values
- If expression evaluates to `true` → field is shown
- If expression evaluates to `false` → field is hidden (not in DOM)
- If expression evaluates to an error → field is hidden (conservative default)
- Re-evaluation triggers on any form field change (debounced 100ms)

### 5.2 Visual Indication

- No visual indication of display logic (hidden = gone)
- User sees field appear/disappear as dependent fields change
- Smooth CSS transition for visibility changes (optional)

### 5.3 Example

```
Status:    [Draft  v]
ApprovalNotes: [______]  ← Hidden when Status = Draft

Status:    [Pending v]
ApprovalNotes: [______]  ← Shown when Status = Pending
```

---

## 6. Dynamic Read-Only

### 6.1 Behavior

- `isReadOnlyOverride=true` → field always read-only
- `readOnlyLogic` → field becomes read-only when expression is true
- Read-only fields show the value but are not editable
- Visual: text displayed, no input border, not focusable

### 6.2 Visual Indication

- Read-only fields: gray background or text-only (no input border)
- Optional: small lock icon next to label

---

## 7. Dynamic Mandatory

### 7.1 Behavior

- `isMandatoryOverride=true` → field always mandatory
- `mandatoryLogic` → field becomes mandatory when expression is true
- Mandatory fields have red asterisk (*) on label
- Form submission checks all mandatory fields

### 7.2 Visual Indication

- Mandatory: red asterisk after label
- Optional: no asterisk
- Dynamic: asterisk appears/disappears as mandatoryLogic evaluates

```
Name: *       [______________]   ← Mandatory
Description:  [______________]   ← Optional
```

---

## 8. Validation Messages

### 8.1 Message Locations

| Validation Type | Location | Visual |
|---|---|---|
| Field-level (mandatory, type, length) | Below the field | Red text, small font |
| Form-level (multiple errors) | Top of form (banner) | Red banner listing all errors |
| API-level (business rule) | Top of form (banner) | Red banner with error details |
| Lookup validation | Inside lookup dialog | Red text below search |

### 8.2 Message Format

```
Field: <Human-readable label>
Rule: <mandatory, type, minLength, maxLength, valRule>
Message: <Human-readable description>
```

Examples:
- "Title is mandatory."
- "Amount must be a valid number."
- "Name must be between 1 and 120 characters."
- "Invalid email format." (from ValRule)

### 8.3 Validation Timing

| Timing | Behavior |
|---|---|
| On blur | Validate field when user leaves it |
| On change | Validate field as user types (debounced 300ms) |
| On submit | Validate all fields before API call |
| Recommended | Blur + submit (best UX, least intrusive) |

---

## 9. Lookup Dialogs

### 9.1 Table Lookup

```
┌────────────────────────────────────────────────┐
│  Author Lookup                              X │
├────────────────────────────────────────────────┤
│  [🔍 Search authors...                    ]    │
├────────────────────────────────────────────────┤
│  ☐  ID   │ Name              │ Email          │
│  ───      ────               ──────            │
│  ☐  1    │ David Thomas      │ david@...      │
│  ☐  2    │ Andrew Hunt       │ andrew@...     │
│  ☐  3    │ Uncle Bob Martin  │ bob@...        │
│                                            │
│  Page 1 of 5  [< 1 2 3 4 5 >]              │
├────────────────────────────────────────────────┤
│                           [Cancel]  [Select]  │
└────────────────────────────────────────────────┘
```

- Modal dialog (drawer from right as alternative)
- Search bar at top
- Data grid with columns from reference metadata
- Row selection (single or multi)
- Pagination for large result sets
- Select button fills the form field with selected value

### 9.2 Search Popup

```
┌─────────────────────┐
│ Author        [__v] │
│                   ▲ │
│ ┌─────────────────┐ │
│ │ David Thomas ▼  │ │
│ │ Andrew Hunt     │ │
│ │ Uncle Bob M.    │ │
│ └─────────────────┘ │
└─────────────────────┘
```

- Inline search, not a modal
- Debounced search (300ms)
- Dropdown shows matching results
- Keyboard navigation (up/down/enter/escape)
- Select fills the field

### 9.3 Lookup Dialog States

| State | Description | Visual |
|---|---|---|
| Empty | No results | "No results found" message |
| Loading | API call in progress | Spinner in grid |
| Error | API error | Error message in grid |
| Search | Search text entered | Search text highlighted |
| Selected | Row selected | Row highlighted |
| Pagination | Multiple pages | Page controls visible |

---

## 10. Tables / Grids

### 10.1 Grid Layout

```
┌──────────────────────────────────────────────────────────┐
│  Filters                              [+ Add] [Columns] │
│  [🔍 Search...]  [Status: All v]  [Date: All v]         │
├──────────────────────────────────────────────────────────┤
│  ☐  Title              │ Author      │ Status   │ Action│
│  ──────────────────────┼─────────────┼──────────┼───────│
│    The Pragmatic...    │ David T.    │ Active   │ [· ·] │
│    Clean Code          │ Andrew H.   │ Archived │ [· ·] │
│    Refactoring...      │ Robert M.   │ Active   │ [· ·] │
│                                                  ↑      │
│                    100 records · Page 1 of 5  [>>>>]    │
└──────────────────────────────────────────────────────────┘
```

### 10.2 Grid Features

- **Sortable columns**: Click header to sort (asc/desc toggle)
- **Filterable**: Filter bar above grid with quick filters + advanced filter
- **Pagination**: Page size selector + page navigation
- **Column visibility**: Toggle columns on/off
- **Row selection**: Checkbox for bulk operations
- **Action column**: Ellipsis menu per row (Edit, Delete, etc.)
- **Responsive**: Horizontal scroll on small screens

### 10.3 Grid States

| State | Description | Visual |
|---|---|---|
| Loading | Data fetching | Spinner in grid area |
| Empty | No data | "No records found" illustration |
| Error | API error | Error banner, retry button |
| Filtering | Active filters | Filter chips shown |
| Sorting | Column sorted | Sort indicator in header |
| Selected | Rows selected | Row highlight, bulk actions appear |
| Pagination | Multi-page | Page controls at bottom |

---

## 11. Filtering

### 11.1 Filter UI

- **Quick filters**: Dropdowns/text inputs for common filter fields
- **Advanced filter**: Builder with AND/OR/NOT grouping
- **Filter chips**: Active filters shown as removable chips

```
Filters  [🔍 Name: %test%] [Status: Active ×] [>+ ]
```

### 11.2 Filter Builder (Advanced)

```
Filter by:
  [Name] [contains] [test]  [×]
  AND
  [Status] [equals] [Active]  [×]
  OR
  [Amount] [greater than] [1000]  [×]

              [Apply]  [Clear All]
```

### 11.3 Filter States

| State | Description |
|---|---|
| No filters | All data shown |
| Active filters | Filter chips displayed |
| Filter applying | Loading spinner, stale data shown |
| Filter error | Error message, retry option |
| No results | "No records match the filters" message |

---

## 12. Sorting

### 12.1 Visual Indication

- Unsorted: no indicator in header
- Ascending: ▲ icon in header
- Descending: ▼ icon in header
- Multi-column: number suffix (1, 2, 3)

### 12.2 Sorting Behavior

- Single column: click to cycle asc → desc → none
- Server-side sort: API call on sort change
- Sort indicator during loading

---

## 13. Pagination

### 13.1 Pagination Controls

```
Showing 1-50 of 250 records  Pages: [50 ▼]  [<<< 1 2 3 4 5 >>>]
```

- Records count: "Showing X-Y of Z"
- Page size selector: 10, 20, 50, 100, 500
- Page navigation: first, previous, page numbers, next, last
- Current page highlighted

### 13.2 Pagination States

| State | Description |
|---|---|
| Single page | Page navigation hidden |
| Multi-page | Page navigation shown |
| Loading | Spinner during page change |

---

## 14. Loading States

| Context | Visual | Duration |
|---|---|---|
| Window load | Full-content spinner | 0-2s |
| Tab switch | Tab-bar spinner | 0-500ms |
| Form load | Form skeleton (shimmer) | 0-2s |
| Grid load | Skeleton rows | 0-2s |
| API call | Loading spinners | 0-5s |
| Slow operation | Progress indicator | >5s |

### 14.1 Skeleton Screens

For initial data load, show skeleton placeholders matching the form/grid layout:
- Form: gray rectangle bars where fields will be
- Grid: gray rectangle rows

---

## 15. Empty States

| Context | Message | Visual |
|---|---|---|
| No records in grid | "No records found" | Illustration + "Create First Record" button |
| No tabs in window | "No tabs configured" | Information icon |
| No fields in tab | "No fields configured" | Information icon |
| No menu items | "No navigation items" | Minimal state |
| Filter produces no results | "No records match your filters" | "Clear Filters" button |
| Window not found | "Window not found" | 404 illustration |

---

## 16. Error States

| Context | Visual | Recovery |
|---|---|---|
| API error (500) | Red banner at top: "An error occurred. Please try again." | Retry button |
| Validation error | Field-level red borders + messages | User corrects and resubmits |
| Network error | Red banner: "Connection lost. Retrying..." | Auto-retry + manual retry |
| 401 Unauthorized | Redirect to login | Re-authenticate |
| 403 Forbidden | "You do not have access to this page." | Contact admin |
| 404 Not found | "Record not found" | Back to list |
| Timeout | "Request timed out. Please try again." | Retry button |

---

## 17. Permission States

| State | Description | Visual |
|---|---|---|
| No window access | User cannot access window | Window not in menu, 403 if URL accessed directly |
| No create permission | Create button hidden | No "+ Add" button in grid |
| No edit permission | Edit button hidden, form read-only | Form fields disabled |
| No delete permission | Delete button hidden | No delete option in row actions |
| No column access | Column not visible | Column excluded from grid/form |

Phase 3 baseline (no auth yet):
- All permissions granted (no auth middleware)
- Structure in place for Phase 4 role-based permissions
- UI components accept permission props: `<Form canCreate={true} canUpdate={true} canDelete={false} />`

---

## 18. Unsaved Changes

### 18.1 Behavior

- Track dirty state via react-hook-form `formState.isDirty`
- On form submit: mark clean
- On field change: mark dirty
- On navigation attempt: if dirty, show confirmation dialog
- On browser close/tab close: `window.onBeforeUnload` warning

### 18.2 Confirmation Dialog

```
┌─────────────────────────────────────────┐
│  Unsaved changes                        │
│                                         │
│  You have unsaved changes.              │
│  Are you sure you want to leave?        │
│                                         │
│                        [Cancel] [Discard]│
└─────────────────────────────────────────┘
```

---

## 19. Confirmation Dialogs

| Action | Dialog Message |
|---|---|
| Delete record | "Are you sure you want to delete this record? This action cannot be undone." |
| Bulk delete | "Are you sure you want to delete {n} records? This action cannot be undone." |
| Discard changes | "You have unsaved changes. Are you sure you want to discard them?" |
| Close window | "You have unsaved changes. Close anyway?" |

---

## 20. Keyboard Navigation

### 20.1 Form Keyboard Shortcuts

| Key | Action |
|---|---|
| Tab | Move to next field |
| Shift+Tab | Move to previous field |
| Enter | Submit form (when on last field or no more fields) |
| Escape | Cancel form (if dirty, show confirmation) |
| Ctrl+S | Save form (alternative to clicking Save) |

### 20.2 Grid Keyboard Navigation

| Key | Action |
|---|---|
| Arrow Down | Move to next row |
| Arrow Up | Move to previous row |
| Space | Select/deselect row |
| Enter | Open selected row for edit |
| Delete | Delete selected row (with confirmation) |
| Ctrl+A | Select all rows on current page |
| / | Focus search/filter input |

### 20.3 Lookup Keyboard Navigation

| Key | Action |
|---|---|
| Arrow Down | Move to next result |
| Arrow Up | Move to previous result |
| Enter | Select current result |
| Escape | Close lookup dialog |

---

## 21. Accessibility

### 21.1 WCAG 2.1 AA Compliance

| Criterion | Implementation |
|---|---|
| Color contrast | Minimum 4.5:1 for text, 3:1 for UI components |
| Focus indicators | Visible focus ring on all interactive elements |
| ARIA labels | Descriptive aria-label on all form controls |
| Screen reader | Semantic HTML, live regions for dynamic content |
| Keyboard | All interactions keyboard-accessible |
| Form labels | Every input has associated `<label>` |
| Error messages | `aria-describedby` links to error text |
| Language | `lang` attribute on HTML element |

### 21.2 ARIA Roles

| Element | ARIA Role |
|---|---|
| Tab bar | `role="tablist"`, tabs have `role="tab"`, content has `role="tabpanel"` |
| Grid | `role="grid"`, rows have `role="row"`, checkboxes have `role="checkbox"` |
| Dialog | `role="dialog"`, `aria-modal="true"` |
| Menu | `role="menubar"`, items have `role="menuitem"` |
| Toast/Notification | `role="alert"`, `aria-live="assertive"` |
| Loading spinner | `role="status"`, `aria-live="polite"` |

---

## 22. Responsive Behavior

### 22.1 Breakpoints

| Breakpoint | Width | Layout |
|---|---|---|
| Mobile | < 768px | Single column, full-width fields, stacked footer buttons |
| Tablet | 768px - 1024px | 2-column fields, responsive grid |
| Desktop | > 1024px | Default layout (colSpan-based) |

### 22.2 Mobile Adaptations

- Form fields: full width on mobile (colSpan ignored)
- Footer buttons: stacked vertically on mobile
- Grid: horizontal scroll for extra columns
- Lookup dialog: full-screen drawer on mobile
- Menu: hamburger navigation on mobile

---

## 23. Loading State Details

| State | Component | Visual | Behavior |
|---|---|---|---|
| Initial load | Window | Full-screen spinner | Fetch window metadata + data |
| Tab switch | Tab | Tab-content spinner | Fetch tab data if lazy-loaded |
| Pagination | Grid | Row-level shimmer | Fetch next page |
| Lookup | Lookup | Spinner in dropdown | Fetch reference data |
| Menu | Menu | Skeleton menu items | Fetch menu hierarchy |
| Slow load (>2s) | Any | Progress bar | Show elapsed time |

---

## 24. Design Token Summary

| Token | Value | Purpose |
|---|---|---|
| Primary color | `#1677FF` (Ant Design default) | Actions, links, active states |
| Success color | `#52C41A` | Success messages, confirmations |
| Warning color | `#FAAD14` | Warnings, attention needed |
| Error color | `#FF4D4F` | Errors, mandatory fields |
| Text primary | `#1F1F1F` | Primary text |
| Text secondary | `#8C8C8C` | Labels, descriptions |
| Border | `#D9D9D9` | Input borders, dividers |
| Background | `#F5F5F5` | Page background |
| Surface | `#FFFFFF` | Card/window background |
| Spacing unit | 8px | All spacing is multiples of 8 |
| Font family | System font stack (platform-native) | Performance and familiarity |
| Font size base | 14px | Body text |
| Font size small | 12px | Labels, help text |
| Font size large | 16px | Section headers |
| Border radius | 6px | Inputs, cards, dialogs |
| Shadow | `0 2px 8px rgba(0,0,0,0.15)` | Cards, dialogs, dropdowns |
