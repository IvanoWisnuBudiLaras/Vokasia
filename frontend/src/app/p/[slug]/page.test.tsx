import { afterAll, expect, mock, test } from "bun:test";
import { publicPortfolioCacheTag } from "@/lib/publicPortfolioCache";
import PublicPortfolioPage from "./page";

const originalFetch = globalThis.fetch;
const originalApiInternalUrl = process.env.API_INTERNAL_URL;
process.env.API_INTERNAL_URL = "http://api.test";

afterAll(() => {
  globalThis.fetch = originalFetch;
  if (originalApiInternalUrl === undefined) {
    delete process.env.API_INTERNAL_URL;
  } else {
    process.env.API_INTERNAL_URL = originalApiInternalUrl;
  }
});

test("public portfolio fetch keeps a five-minute TTL and a per-slug cache tag", async () => {
  const slug = "siswa-contoh-rpl-2026";
  let requestedUrl: string | null = null;
  let requestedOptions: RequestInit | undefined;

  globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
    requestedUrl = String(input);
    requestedOptions = init;
    return Response.json({
      studentName: "Siswa Contoh",
      schoolName: "SMK Nusantara",
      majorName: "Rekayasa Perangkat Lunak",
      year: 2026,
      companyName: "PT Teknologi Maju",
      durationLabel: "6 bulan",
      verifiedCompetencies: ["Pemrograman Web"],
      sampleThumbnailUrls: ["https://objects.test/sample.webp"],
      hasCertificate: true,
    });
  }) as typeof fetch;

  await PublicPortfolioPage({ params: Promise.resolve({ slug }) });

  expect(requestedUrl).toBe(`http://api.test/p/${slug}`);
  expect(requestedOptions?.next).toEqual({
    revalidate: 300,
    tags: [publicPortfolioCacheTag(slug)],
  });
});
