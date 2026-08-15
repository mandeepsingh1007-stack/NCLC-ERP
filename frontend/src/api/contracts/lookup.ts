/**
 * Lookup API contracts matching Phase 3 LookupEndpoints responses.
 */

import { PaginationInfo } from './data';

export interface LookupItem {
  value: string | number;
  display: string;
}

export interface ListLookupResponse {
  totalItems: number;
  pagination: PaginationInfo;
  items: LookupItem[];
}

export interface TableLookupResponse {
  referenceName: string;
  targetTable: string;
  totalItems: number;
  pagination: PaginationInfo;
  items: { value: string | number; display: string }[];
}

export type LookupResponse = ListLookupResponse | TableLookupResponse;

export interface LookupQueryParams {
  page?: number;
  pageSize?: number;
  search?: string;
}
