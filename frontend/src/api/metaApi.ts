/**
 * TanStack Query hooks for Meta API endpoints.
 */
import { useQuery } from '@tanstack/react-query';
import api from './client';
import type { WindowContract, WindowsListResponse, MenuHierarchyResponse } from './contracts/window';

export const metaKeys = {
  all: ['meta'] as const,
  window: (id: number) => {
    const base: readonly string[] = ['meta'];
    return [...base, 'window', id] as const;
  },
  windows: ['meta', 'windows'] as const,
  menu: ['meta', 'menu'] as const,
};

export function useWindow(windowId: number) {
  return useQuery<WindowContract>({
    queryKey: metaKeys.window(windowId),
    queryFn: () => api.get(`/meta/window/${windowId}`).then((r) => r.data),
    staleTime: Infinity, // metadata is permanent until server invalidates
  });
}

export function useWindows() {
  return useQuery<WindowsListResponse>({
    queryKey: metaKeys.windows,
    queryFn: () => api.get('/meta/windows').then((r) => r.data),
    staleTime: 5 * 60 * 1000,
  });
}

export function useMenu() {
  return useQuery<MenuHierarchyResponse>({
    queryKey: metaKeys.menu,
    queryFn: () => api.get('/meta/menu').then((r) => r.data),
    staleTime: 5 * 60 * 1000,
  });
}
