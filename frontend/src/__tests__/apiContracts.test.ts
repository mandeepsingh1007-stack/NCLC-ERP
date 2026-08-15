/**
 * API Contract Tests
 *
 * Verifies that TypeScript contracts match expected API response shapes.
 * These are structural tests — no server required.
 *
 * Contracts:
 *   src/api/contracts/window.ts
 *   src/api/contracts/data.ts
 *   src/api/contracts/lookup.ts
 */

import type {
  WindowContract,
  TabContract,
  FieldContract,
  FieldGroupContract,
  WindowsListResponse,
  MenuContract,
  MenuHierarchyResponse,
} from '../api/contracts/window';
import type {
  DataTableResponse,
  PaginationInfo,
  ErrorResponse,
} from '../api/contracts/data';
import type {
  LookupItem,
  ListLookupResponse,
  TableLookupResponse,
  LookupResponse,
} from '../api/contracts/lookup';

// ─── Helper: structural shape check ───────────────────────────────────

function assertShape(name: string, obj: unknown, keys: string[]) {
  const actualKeys = Object.keys(obj as object);
  for (const key of keys) {
    if (!actualKeys.includes(key)) {
      throw new Error(`${name}: missing required key "${key}". Actual keys: [${actualKeys.join(', ')}]`);
    }
  }
}

// ─── Window Contract ──────────────────────────────────────────────────

describe('Window Contract', () => {
  it('has valid window contract shape', () => {
    const window: WindowContract = {
      windowId: 1,
      columnName: 'X_Account',
      name: 'Account',
      description: 'Account window',
      help: 'Manage accounts',
      tabs: [
        {
          tabId: 1,
          columnName: 'Main',
          name: 'Main',
          sysTableId: 1,
          isGrid: false,
          isDefault: true,
          whereClause: '1=1',
          fields: [
            {
              columnName: 'Name',
              label: 'Name',
              controlType: 'TextInput',
              isMandatory: true,
              isReadOnly: false,
              isMandatoryOverride: false,
              isReadOnlyOverride: false,
              colSpan: 6,
              rowSpan: 1,
              defaultValue: '',
              displayLogic: '$Active == true',
              help: 'Account name',
            } satisfies FieldContract,
          ],
          fieldGroups: [
            {
              groupName: 'Main',
              label: 'Main',
              colSpan: 12,
              isCollapsed: false,
              fieldColumnNames: ['Name'],
            } satisfies FieldGroupContract,
          ],
        } satisfies TabContract,
      ],
    };

    assertShape('window', window, ['windowId', 'columnName', 'name', 'tabs']);
    assertShape('tab', window.tabs[0], ['tabId', 'columnName', 'name', 'sysTableId', 'isGrid', 'isDefault', 'fields', 'fieldGroups']);
    assertShape('field', window.tabs[0].fields[0], [
      'columnName', 'label', 'controlType', 'isMandatory', 'isReadOnly',
      'isMandatoryOverride', 'isReadOnlyOverride', 'colSpan', 'rowSpan',
    ]);
    assertShape('fieldGroup', window.tabs[0].fieldGroups[0], [
      'groupName', 'label', 'colSpan', 'isCollapsed', 'fieldColumnNames',
    ]);
  });

  it('validates ControlType union', () => {
    const validTypes: FieldContract['controlType'][] = [
      'TextInput', 'TextArea', 'NumberInput', 'DateInput', 'YesNoToggle',
      'ListDropdown', 'TableLookup', 'SearchPopup', 'MultiSelect',
      'Email', 'URL', 'Password', 'RichText', 'Image', 'FileUpload',
      'Date', 'Time', 'DateTime',
    ];
    expect(validTypes.length).toBeGreaterThan(10);
    // All must be string literals
    for (const t of validTypes) {
      expect(typeof t).toBe('string');
    }
  });

  it('has valid windows list response shape', () => {
    const resp: WindowsListResponse = {
      windows: [
        { windowId: 1, columnName: 'X_Account', name: 'Account' },
        { windowId: 2, columnName: 'X_Contact', name: 'Contact', description: 'Contacts' },
      ],
    };
    expect(resp.windows).toBeInstanceOf(Array);
    expect(resp.windows[0]).toHaveProperty('windowId');
    expect(resp.windows[0]).toHaveProperty('columnName');
    expect(resp.windows[0]).toHaveProperty('name');
  });
});

// ─── Menu Contract ────────────────────────────────────────────────────

describe('Menu Contract', () => {
  it('has valid menu contract shape', () => {
    const menu: MenuContract = {
      menuId: 1,
      columnName: 'M_Main',
      name: 'Main Menu',
      icon: 'HomeOutlined',
      sequence: 1,
      parentId: null,
      windowId: 1,
      processId: null,
      isSeparator: false,
      children: [
        {
          menuId: 2,
          columnName: 'M_Accounts',
          name: 'Accounts',
          icon: 'TeamOutlined',
          sequence: 1,
          parentId: 1,
          windowId: null,
          processId: null,
          isSeparator: false,
          children: [],
        },
      ],
    };

    assertShape('menu', menu, ['menuId', 'columnName', 'name', 'parentId', 'windowId', 'children']);
    expect(menu.children.length).toBe(1);
  });

  it('has valid menu hierarchy response shape', () => {
    const resp: MenuHierarchyResponse = {
      items: [],
    };
    expect(resp.items).toBeInstanceOf(Array);
  });
});

// ─── Data Contract ────────────────────────────────────────────────────

describe('Data Contract', () => {
  it('has valid pagination info shape', () => {
    const pagination: PaginationInfo = {
      page: 1,
      pageSize: 20,
      totalItems: 100,
      totalPages: 5,
    };
    expect(pagination.page).toBe(1);
    expect(pagination.totalPages).toBe(5);
  });

  it('has valid data table response shape', () => {
    const resp: DataTableResponse<{ Id: number; Name: string }> = {
      items: [{ Id: 1, Name: 'Test' }],
      pagination: { page: 1, pageSize: 20, totalItems: 1, totalPages: 1 },
    };
    expect(resp.items).toBeInstanceOf(Array);
    expect(resp.items[0]).toHaveProperty('Id');
    expect(resp.items[0]).toHaveProperty('Name');
    assertShape('pagination', resp.pagination, ['page', 'pageSize', 'totalItems', 'totalPages']);
  });

  it('has valid error response shape', () => {
    const err: ErrorResponse = {
      error: { code: 'ValidationFailed', message: 'Required field missing' },
    };
    expect(err.error.code).toBe('ValidationFailed');
    expect(err.error.message).toBeDefined();
  });
});

// ─── Lookup Contract ──────────────────────────────────────────────────

describe('Lookup Contract', () => {
  it('has valid lookup item shape', () => {
    const item: LookupItem = { value: '1', display: 'One' };
    expect(item).toHaveProperty('value');
    expect(item).toHaveProperty('display');
  });

  it('has valid list lookup response shape', () => {
    const resp: ListLookupResponse = {
      totalItems: 2,
      pagination: { page: 1, pageSize: 10, totalItems: 2, totalPages: 1 },
      items: [
        { value: '1', display: 'One' },
        { value: '2', display: 'Two' },
      ],
    };
    expect(resp.items.length).toBe(2);
    expect(resp.totalItems).toBe(2);
  });

  it('has valid table lookup response shape', () => {
    const resp: TableLookupResponse = {
      referenceName: 'X_Account',
      targetTable: 'X_Account',
      totalItems: 1,
      pagination: { page: 1, pageSize: 10, totalItems: 1, totalPages: 1 },
      items: [{ value: 1, display: 'Test Account' }],
    };
    expect(resp.referenceName).toBe('X_Account');
    expect(resp.targetTable).toBe('X_Account');
  });

  it('LookupResponse union accepts both types', () => {
    const list: LookupResponse = {
      totalItems: 1,
      pagination: { page: 1, pageSize: 10, totalItems: 1, totalPages: 1 },
      items: [{ value: '1', display: 'One' }],
    };
    expect(list).toHaveProperty('totalItems');

    const table: LookupResponse = {
      referenceName: 'X_Test',
      targetTable: 'X_Test',
      totalItems: 1,
      pagination: { page: 1, pageSize: 10, totalItems: 1, totalPages: 1 },
      items: [{ value: 1, display: 'Test' }],
    };
    expect(table).toHaveProperty('referenceName');
    expect(table).toHaveProperty('targetTable');
  });
});
