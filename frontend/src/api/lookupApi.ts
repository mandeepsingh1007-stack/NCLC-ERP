/**
 * TanStack Query hooks for Lookup API endpoints.
 */
import { useQuery } from '@tanstack/react-query';
import api from './client';
import type { LookupResponse, LookupQueryParams } from './contracts/lookup';

export const lookupKeys = {
  all: ['lookup'] as const,
  item: (refId: number, search?: string) => {
    const base: readonly string[] = ['lookup'];
    return [...base, refId, search ?? ''] as const;
  },
};

export function useLookup(referenceId: number, params?: LookupQueryParams) {
  return useQuery<LookupResponse>({
    queryKey: lookupKeys.item(referenceId, params?.search),
    queryFn: () =>
      api.get(`/lookup/${referenceId}`, { params }).then((r) => r.data),
    staleTime: 5 * 60 * 1000,
  });
}
