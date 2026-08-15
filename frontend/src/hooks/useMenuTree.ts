/**
 * Transforms flat menu hierarchy into tree structure for Ant Design Menu.
 */
import { useMemo } from 'react';
import type { MenuContract } from '../api/contracts/window';

export interface MenuTreeNode {
  key: string;
  label: string;
  parentId?: number | null;
  icon?: string;
  children: MenuTreeNode[];
  isSeparator: boolean;
  windowId?: number;
  sequence?: number;
}

export function useMenuTree(menuItems: MenuContract[]): MenuTreeNode[] {
  return useMemo(() => {
    if (!menuItems.length) return [];

    // Build map with raw parentId
    const map = new Map<string, MenuTreeNode>();
    menuItems.forEach((item) => {
      map.set(String(item.menuId), {
        key: String(item.menuId),
        label: item.name,
        parentId: item.parentId,
        icon: item.icon,
        children: [],
        isSeparator: item.isSeparator,
        windowId: item.windowId ?? undefined,
        sequence: item.sequence,
      });
    });

    // Build tree
    const roots: MenuTreeNode[] = [];
    map.forEach((node) => {
      if (node.parentId !== null && map.has(String(node.parentId))) {
        map.get(String(node.parentId))!.children.push(node);
      } else {
        roots.push(node);
      }
    });

    // Sort by sequence
    roots.sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0));
    roots.forEach((r) => sortChildren(r));
    return roots;
  }, [menuItems]);
}

function sortChildren(node: MenuTreeNode) {
  node.children.sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0));
  node.children.forEach(sortChildren);
}
