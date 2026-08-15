/**
 * MenuNavigation — renders hierarchical menu from SysMenu metadata.
 */
import React from 'react';
import { Menu as AntdMenu, type MenuProps } from 'antd';
import type { MenuTreeNode } from '../hooks/useMenuTree';

interface Props {
  /** Pre-built tree (use this or let it fetch and build its own). */
  tree?: MenuTreeNode[];
  onSelect?: (menuId: number, windowId: number | null) => void;
}

const MenuNavigation: React.FC<Props> = ({ tree: propTree, onSelect }) => {
  // Tree is provided by parent (AppLayout) — avoids double-fetch
  const tree = propTree ?? [];

  const handleClick: MenuProps['onClick'] = (info) => {
    const menuItem = findNodeByKey(tree, info.key);
    if (menuItem && onSelect && menuItem.windowId) {
      onSelect(Number(menuItem.key), menuItem.windowId);
    }
  };

  if (tree.length === 0) {
    return null; // Empty menu — nothing to render
  }

  return (
    <AntdMenu
      mode="inline"
      selectedKeys={[]}
      onClick={handleClick}
      items={buildMenuItems(tree)}
    />
  );
};

/** Recursively find a node by key. */
function findNodeByKey(nodes: MenuTreeNode[], key: string): MenuTreeNode | null {
  for (const node of nodes) {
    if (node.key === key) return node;
    const found = findNodeByKey(node.children, key);
    if (found) return found;
  }
  return null;
}

/** Convert MenuTreeNode[] to Ant Design Menu items. */
function buildMenuItems(nodes: MenuTreeNode[]): { key: string; label: string; icon?: string; disabled?: boolean; children?: unknown[] }[] {
  return nodes.map((node) => {
    if (node.isSeparator) {
      return { key: `sep-${node.key}`, label: '', disabled: true };
    }
    const item: { key: string; label: string; icon?: string; disabled?: boolean; children?: unknown[] } = {
      key: node.key,
      label: node.label,
    };
    if (node.icon) {
      item.label = node.label; // icon would need @ant-design/icons import
    }
    if (node.children.length > 0) {
      item.children = buildMenuItems(node.children);
    }
    return item;
  });
}

export default MenuNavigation;
