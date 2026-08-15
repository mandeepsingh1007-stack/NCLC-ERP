/**
 * TanStack Query hooks for Data API endpoints.
 */
import { useQuery } from '@tanstack/react-query';
import api from './client';
import type { DataTableResponse, DataQueryParams } from './contracts/data';

export const dataKeys = {
  all: ['data'] as const,
  table: (table: string, params?: DataQueryParams) => {
    const base: readonly string[] = ['data'];
    return [...base, table, params ?? {}] as const;
  },
  item: (table: string, id: number) => {
    const base: readonly string[] = ['data'];
    return [...base, table, id] as const;
  },
};

export function useDataTable(table: string, params?: DataQueryParams) {
  return useQuery<DataTableResponse>({
    queryKey: dataKeys.table(table, params),
    queryFn: () =>
      api.get(`/data/${table}`, { params }).then((r) => r.data),
    staleTime: 0, // data is volatile
  });
}

export function useDataItem(table: string, id: number) {
  return useQuery<Record<string, unknown>>({
    queryKey: dataKeys.item(table, id),
    queryFn: () =>
      api.get(`/data/${table}/${id}`).then((r) => r.data),
    enabled: id > 0,
    staleTime: 0,
  });
}
