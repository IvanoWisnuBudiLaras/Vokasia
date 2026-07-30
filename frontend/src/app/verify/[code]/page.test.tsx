import { afterAll, expect, mock, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import VerifyCertificatePage from "./page";

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

test("valid certificate response renders the public success state without sensitive data", async () => {
  let requestedUrl: string | null = null;
  let requestedOptions: RequestInit | undefined;

  globalThis.fetch = mock(
    async (input: string | URL | Request, init?: RequestInit) => {
      requestedUrl = String(input);
      requestedOptions = init;
      return Response.json({
        studentName: "Siswa Contoh",
        schoolName: "SMK Nusantara",
        companyName: "PT Teknologi Maju",
        periodLabel: "Januari–Juni 2026",
        issuedAt: "2026-07-29T06:30:00Z",
        valid: true,
      });
    },
  ) as typeof fetch;

  const page = await VerifyCertificatePage({
    params: Promise.resolve({ code: "VOK/2026 A+B" }),
  });
  const html = renderToStaticMarkup(page);

  expect(requestedUrl).toBe("http://api.test/api/verify/VOK%2F2026%20A%2BB");
  expect(requestedOptions?.next?.revalidate).toBe(0);
  expect(html).toContain("Sertifikat terverifikasi");
  expect(html).toContain("Siswa Contoh");
  expect(html).toContain("SMK Nusantara");
  expect(html).toContain("PT Teknologi Maju");
  expect(html).toContain("Januari–Juni 2026");
  expect(html).toContain("text-status-green");
  expect(html).toContain("border-status-green");
  expect(html).toContain("divide-status-green");
  expect(html).not.toContain("border-status-green/");
  expect(html).toContain("text-base");
  expect(html).not.toContain("text-sm");
  expect(html).not.toContain("Sertifikat tidak ditemukan");
  expect(html).not.toContain("NISN");
  expect(html).not.toContain("email");
});

test("404 response renders a helpful failure state with the submitted code", async () => {
  globalThis.fetch = mock(async () => new Response(null, { status: 404 })) as typeof fetch;

  const page = await VerifyCertificatePage({
    params: Promise.resolve({ code: "TIDAK-ADA" }),
  });
  const html = renderToStaticMarkup(page);

  expect(html).toContain("Sertifikat tidak ditemukan");
  expect(html).toContain("TIDAK-ADA");
  expect(html).toContain("Periksa kembali kode");
  expect(html).toContain("text-status-red");
  expect(html).toContain("border-status-red");
  expect(html).not.toContain("border-status-red/");
  expect(html).toContain("text-base");
  expect(html).not.toContain("text-sm");
});
