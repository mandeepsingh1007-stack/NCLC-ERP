/**
 * React hook for client-side display logic evaluation.
 * Caches evaluated results to avoid re-parsing on every render.
 */
import { useMemo } from 'react';
import { evaluateDisplayLogic, DisplayLogicContext } from '../utils/displayLogicEval';

export function useDisplayLogic(
  expression: string | null | undefined,
  context: DisplayLogicContext,
  formData: Record<string, unknown>,
): boolean {
  return useMemo(
    () => evaluateDisplayLogic(expression ?? null, context, formData),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [expression, formData.$UserId, formData.$TenantId, formData.$OrgId, formData.$Timestamp, formData.$UserName, ...Object.values(formData)],
  );
}
