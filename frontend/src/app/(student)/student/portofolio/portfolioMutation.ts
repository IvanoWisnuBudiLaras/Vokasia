import { ApiError } from "@/lib/apiClient";
import type { PortfolioDto } from "@/lib/apiTypes";

interface ReconciledPortfolioMutation {
  portfolio: PortfolioDto;
  notice: string;
  reconciled: boolean;
}

function mutationWasApplied(error: unknown): error is ApiError {
  if (!(error instanceof ApiError) || !error.body || typeof error.body !== "object") {
    return false;
  }

  return (error.body as Record<string, unknown>).mutationApplied === true;
}

/**
 * A cache/status follow-up can fail after the API has already committed a portfolio mutation.
 * In that case a stale client state is unsafe: publish could look private, or unpublish active.
 * Prefer the authoritative private endpoint and retain an operation-safe fallback if it is down.
 */
export async function reconcileAppliedPortfolioMutation(
  error: unknown,
  optimisticPortfolio: PortfolioDto,
  loadLatest: () => Promise<PortfolioDto>,
  notice: string
): Promise<ReconciledPortfolioMutation | null> {
  if (!mutationWasApplied(error)) return null;

  try {
    return {
      portfolio: await loadLatest(),
      notice,
      reconciled: true,
    };
  } catch {
    return {
      portfolio: optimisticPortfolio,
      notice,
      reconciled: false,
    };
  }
}
