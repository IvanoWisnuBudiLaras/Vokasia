import { describe, expect, mock, test } from "bun:test";
import { ApiError } from "@/lib/apiClient";
import type { PortfolioDto } from "@/lib/apiTypes";
import { reconcileAppliedPortfolioMutation } from "./portfolioMutation";

const portfolio = (isPublished: boolean, slug: string | null): PortfolioDto => ({
  headline: "Siap kerja di bidang web",
  verifiedCompetencies: ["Pemrograman Web"],
  sampleJournals: [],
  certificate: null,
  isPublished,
  slug,
});

describe("reconcileAppliedPortfolioMutation", () => {
  test("ignores ordinary request failures and does not load state", async () => {
    const loadLatest = mock(async () => portfolio(false, null));
    const error = new ApiError(502, { mutationApplied: false }, "Perubahan gagal.");

    const result = await reconcileAppliedPortfolioMutation(
      error,
      portfolio(false, null),
      loadLatest,
      "Status perlu diperiksa."
    );

    expect(result).toBeNull();
    expect(loadLatest).not.toHaveBeenCalled();
  });

  test("uses authoritative state after an applied mutation", async () => {
    const latest = portfolio(false, "budi-santoso-rpl-2026");
    const error = new ApiError(
      502,
      { mutationApplied: true },
      "Publikasi sudah dinonaktifkan."
    );

    const result = await reconcileAppliedPortfolioMutation(
      error,
      portfolio(true, latest.slug),
      async () => latest,
      "Publikasi sudah dinonaktifkan; periksa kembali sebelum membagikan tautan."
    );

    expect(result).toEqual({
      portfolio: latest,
      notice: "Publikasi sudah dinonaktifkan; periksa kembali sebelum membagikan tautan.",
      reconciled: true,
    });
  });

  test("keeps the operation-safe optimistic state when reload also fails", async () => {
    const optimistic = portfolio(true, null);
    const error = new ApiError(
      502,
      { mutationApplied: true },
      "Portofolio sudah dipublikasikan."
    );

    const result = await reconcileAppliedPortfolioMutation(
      error,
      optimistic,
      async () => {
        throw new Error("portfolio lookup unavailable");
      },
      "Portofolio sudah dipublikasikan, tetapi tautannya belum bisa dimuat."
    );

    expect(result).toEqual({
      portfolio: optimistic,
      notice: "Portofolio sudah dipublikasikan, tetapi tautannya belum bisa dimuat.",
      reconciled: false,
    });
  });
});
