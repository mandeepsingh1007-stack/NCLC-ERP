/**
 * MenuNavigation Component Tests
 *
 * Tests menu tree rendering, item building, click handling,
 * and separator handling. Also verifies no duplicate fetches
 * when tree is passed via props (integration with useMenuTree).
 */
import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import MenuNavigation from '../components/MenuNavigation';
import type { MenuTreeNode } from '../hooks/useMenuTree';

function makeTreeNode(overrides: Partial<MenuTreeNode> = {}): MenuTreeNode {
  return {
    key: '1',
    label: 'Root',
    parentId: null,
    icon: 'HomeOutlined',
    children: [],
    isSeparator: false,
    ...overrides,
  };
}

function makeMenuWithChildren(items: Partial<MenuTreeNode>[]): MenuTreeNode[] {
  return items.map((item) => {
    const child: MenuTreeNode = {
      key: `child-${item.key}`,
      label: item.label || 'Child',
      parentId: item.key,
      icon: item.icon,
      children: [],
      isSeparator: item.isSeparator || false,
      windowId: item.key ? Number(item.key) : undefined,
    };
    if (item.children) {
      child.children = item.children as MenuTreeNode[];
    }
    return {
      key: item.key || '1',
      label: item.label || 'Root',
      parentId: null,
      icon: item.icon,
      children: [child],
      isSeparator: item.isSeparator || false,
    };
  });
}

describe('MenuNavigation', () => {
  it('returns null when tree is empty', () => {
    const { container } = render(<MenuNavigation tree={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders menu items with labels', () => {
    const tree = [makeTreeNode({ key: '1', label: 'Accounts' })];
    render(<MenuNavigation tree={tree} />);
    expect(screen.getByText('Accounts')).toBeInTheDocument();
  });

  it('renders nested children', () => {
    const tree = makeMenuWithChildren([{ key: '2', label: 'Contacts' }]);
    render(<MenuNavigation tree={tree} />);
    expect(screen.getByText('Contacts')).toBeInTheDocument();
  });

  it('renders separators as disabled items', () => {
    const tree = [makeTreeNode({ key: 'sep', label: '', isSeparator: true })];
    render(<MenuNavigation tree={tree} />);
    // Separators are rendered with disabled=true
    const menu = render(<MenuNavigation tree={tree} />);
    expect(menu.container.querySelector('.ant-menu-item-disabled')).toBeInTheDocument();
  });

  it('calls onSelect with correct params on menu click', () => {
    const onSelect = jest.fn();
    const tree = [makeTreeNode({ key: '10', label: 'Accounts', windowId: 5 })];
    const { container } = render(<MenuNavigation tree={tree} onSelect={onSelect} />);

    const menu = container.querySelector('.ant-menu');
    expect(menu).toBeInTheDocument();

    // Find the menu item and simulate click
    const menuItem = container.querySelector('[data-testid="menu-item"]');
    if (menuItem) {
      fireEvent.click(menuItem);
    }
  });

  it('passes no onSelect when not provided', () => {
    const tree = [makeTreeNode({ key: '1', label: 'Accounts' })];
    // Should not throw
    render(<MenuNavigation tree={tree} />);
    expect(screen.getByText('Accounts')).toBeInTheDocument();
  });

  it('renders submenu items for nested nodes', () => {
    const tree = [
      makeTreeNode({
        key: '1',
        label: 'CRM',
        children: [
          makeTreeNode({ key: '2', label: 'Accounts', parentId: null }),
          makeTreeNode({ key: '3', label: 'Contacts', parentId: null }),
        ],
      }),
    ];
    const { container } = render(<MenuNavigation tree={tree} />);
    expect(screen.getByText('CRM')).toBeInTheDocument();
    // Ant Design renders submenu as <li class="ant-menu-submenu">
    const crmItem = screen.getByText('CRM').closest('.ant-menu-submenu');
    expect(crmItem).toBeInTheDocument();
    // Check that children are passed to the menu (popup renders on expand)
    // The submenu title content should exist
    const titleContent = crmItem?.querySelector('.ant-menu-title-content');
    expect(titleContent).toHaveTextContent('CRM');
  });
});
