/**
 * Data API contracts matching Phase 3 DataEndpoints responses.
 */

export interface PaginationInfo {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface DataTableResponse<T = Record<string, unknown>> {
  items: T[];
  pagination: PaginationInfo;
}

export interface DataQueryParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  filter?: string; // JSON filter AST
  columns?: string; // comma-separated column names
}

export interface ErrorResponse {
  error: {
    code: string;
    message: string;
    details?: unknown;
  };
}
