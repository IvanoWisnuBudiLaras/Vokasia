import { afterAll, beforeEach, describe, expect, mock, test } from "bun:test";
import { NextRequest } from "next/server";
import { publicPortfolioCacheTag } from "@/lib/publicPortfolioCache";

const events: string[] = [];
const revalidateTagMock = mock((tag: string, profile: { expire: number }) => {
  events.push(`REVALIDATE ${tag} ${JSON.stringify(profile)}`);
});
const refreshOnExpiryMock = mock(async () => ({ ok: true as const, accessToken: "refreshed-access-token" }));

mock.module("next/cache", () => ({
  revalidateTag: revalidateTagMock,
}));

mock.module("next/headers", () => ({
  cookies: async () => ({
    get: (name: string) => (name === "vok_sess" ? { value: "signed-session-cookie" } : undefined),
  }),
}));

mock.module("@/lib/bffSession", () => ({
  SESS_COOKIE_NAME: "vok_sess",
  decodeSessCookie: () => "session-id",
  getSessionData: async () => ({
    accessToken: "access-token",
    accessExp: Date.now() + 60_000,
    refreshToken: "refresh-token",
    user: {
      id: "student-user-id",
      name: "Budi Santoso",
      role: "Student",
      tenantId: "tenant-id",
    },
  }),
}));

mock.module("@/lib/refresh", () => ({
  refreshOnExpiry: refreshOnExpiryMock,
}));

const originalFetch = globalThis.fetch;
const originalApiInternalUrl = process.env.API_INTERNAL_URL;
process.env.API_INTERNAL_URL = "http://api.test";

const { POST, PUT } = await import("./route");

function routeContext(...path: string[]) {
  return { params: Promise.resolve({ path }) };
}

function request(method: "POST" | "PUT", path: string): NextRequest {
  return new NextRequest(`http://app.test/api/proxy/${path}`, { method });
}

function privatePortfolio(slug: string) {
  return {
    headline: "Siap kerja di bidang web",
    verifiedCompetencies: ["Pemrograman Web"],
    sampleJournals: [
      {
        journalEntryId: "journal-id",
        text: "Membuat halaman responsif.",
        submittedAt: "2026-07-29T01:00:00Z",
      },
    ],
    certificate: null,
    isPublished: true,
    slug,
  };
}

beforeEach(() => {
  events.length = 0;
  revalidateTagMock.mockClear();
  refreshOnExpiryMock.mockClear();
});

afterAll(() => {
  globalThis.fetch = originalFetch;
  if (originalApiInternalUrl === undefined) {
    delete process.env.API_INTERNAL_URL;
  } else {
    process.env.API_INTERNAL_URL = originalApiInternalUrl;
  }
});

describe("portfolio cache invalidation in the authenticated BFF request", () => {
  test("unpublish resolves the server-owned slug before mutation and expires its public tag after 2xx", async () => {
    const slug = "budi-santoso-rpl-2026";

    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);

      if (init?.method === "GET" && url.pathname === "/api/portfolio") {
        return Response.json(privatePortfolio(slug));
      }

      return new Response(null, { status: 204 });
    }) as typeof fetch;

    const response = await POST(request("POST", "portfolio/unpublish"), routeContext("portfolio", "unpublish"));

    expect(response.status).toBe(204);
    expect(events).toEqual([
      "GET /api/portfolio",
      "POST /api/portfolio/unpublish",
      `REVALIDATE ${publicPortfolioCacheTag(slug)} {"expire":0}`,
    ]);
  });

  test("publish expires the public tag from the successful upstream response before returning it", async () => {
    const slug = "budi-santoso-rpl-2026";

    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);
      return Response.json({ slug });
    }) as typeof fetch;

    const response = await POST(request("POST", "portfolio/publish"), routeContext("portfolio", "publish"));

    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ slug });
    expect(events).toEqual([
      "POST /api/portfolio/publish",
      `REVALIDATE ${publicPortfolioCacheTag(slug)} {"expire":0}`,
    ]);
  });

  test("updating an already-published portfolio expires its public tag after the update succeeds", async () => {
    const slug = "budi-santoso-rpl-2026";

    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);

      if (init?.method === "GET" && url.pathname === "/api/portfolio") {
        return Response.json(privatePortfolio(slug));
      }

      return new Response(null, { status: 204 });
    }) as typeof fetch;

    const response = await PUT(request("PUT", "portfolio"), routeContext("portfolio"));

    expect(response.status).toBe(204);
    expect(events).toEqual([
      "PUT /api/portfolio",
      "GET /api/portfolio",
      `REVALIDATE ${publicPortfolioCacheTag(slug)} {"expire":0}`,
    ]);
  });

  test("reuses a token refreshed during update for the following private lookup", async () => {
    const slug = "budi-santoso-rpl-2026";

    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      const authorization = new Headers(init?.headers).get("Authorization");
      events.push(`${init?.method} ${url.pathname} ${authorization}`);

      if (init?.method === "PUT" && authorization === "Bearer access-token") {
        return new Response(null, { status: 401 });
      }

      if (init?.method === "GET") {
        return Response.json(privatePortfolio(slug));
      }

      return new Response(null, { status: 204 });
    }) as typeof fetch;

    const response = await PUT(request("PUT", "portfolio"), routeContext("portfolio"));

    expect(response.status).toBe(204);
    expect(events).toEqual([
      "PUT /api/portfolio Bearer access-token",
      "PUT /api/portfolio Bearer refreshed-access-token",
      "GET /api/portfolio Bearer refreshed-access-token",
      `REVALIDATE ${publicPortfolioCacheTag(slug)} {"expire":0}`,
    ]);
  });

  test("checks an unpublished portfolio after update without expiring its retained slug", async () => {
    const slug = "budi-santoso-rpl-2026";

    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);

      if (init?.method === "GET" && url.pathname === "/api/portfolio") {
        return Response.json({ ...privatePortfolio(slug), isPublished: false });
      }

      return new Response(null, { status: 204 });
    }) as typeof fetch;

    const response = await PUT(request("PUT", "portfolio"), routeContext("portfolio"));

    expect(response.status).toBe(204);
    expect(events).toEqual(["PUT /api/portfolio", "GET /api/portfolio"]);
  });

  test("expires the tag when a concurrent publish lands before the post-update lookup", async () => {
    const slug = "budi-santoso-rpl-2026";

    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);

      if (init?.method === "GET" && url.pathname === "/api/portfolio") {
        return Response.json(privatePortfolio(slug));
      }

      events.push("CONCURRENT PUBLISH");
      return new Response(null, { status: 204 });
    }) as typeof fetch;

    const response = await PUT(request("PUT", "portfolio"), routeContext("portfolio"));

    expect(response.status).toBe(204);
    expect(events).toEqual([
      "PUT /api/portfolio",
      "CONCURRENT PUBLISH",
      "GET /api/portfolio",
      `REVALIDATE ${publicPortfolioCacheTag(slug)} {"expire":0}`,
    ]);
  });

  test("reports an applied update when its post-mutation publication lookup fails", async () => {
    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);

      if (init?.method === "GET" && url.pathname === "/api/portfolio") {
        return Response.json({ message: "Layanan portofolio tidak tersedia." }, { status: 503 });
      }

      return new Response(null, { status: 204 });
    }) as typeof fetch;

    const response = await PUT(request("PUT", "portfolio"), routeContext("portfolio"));

    expect(response.status).toBe(502);
    expect(await response.json()).toEqual({
      code: "PUBLIC_PORTFOLIO_CACHE_CONTEXT_INVALID",
      message: "Perubahan portofolio sudah tersimpan, tetapi status publikasinya tidak dapat diverifikasi. Coba lagi.",
      mutationApplied: true,
    });
    expect(events).toEqual(["PUT /api/portfolio", "GET /api/portfolio"]);
  });

  test("passes through a failed publish without expiring any public tag", async () => {
    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);
      return Response.json({ message: "Portofolio ditolak." }, { status: 422 });
    }) as typeof fetch;

    const response = await POST(request("POST", "portfolio/publish"), routeContext("portfolio", "publish"));

    expect(response.status).toBe(422);
    expect(await response.json()).toEqual({ message: "Portofolio ditolak." });
    expect(events).toEqual(["POST /api/portfolio/publish"]);
  });

  test("unpublish is not sent when the BFF cannot resolve the server-owned slug first", async () => {
    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);
      return Response.json({ message: "Layanan portofolio tidak tersedia." }, { status: 503 });
    }) as typeof fetch;

    const response = await POST(request("POST", "portfolio/unpublish"), routeContext("portfolio", "unpublish"));

    expect(response.status).toBe(503);
    expect(events).toEqual(["GET /api/portfolio"]);
  });

  test("does not mutate when the private portfolio lookup returns malformed JSON", async () => {
    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);
      return new Response("{not-json", {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }) as typeof fetch;

    const response = await POST(request("POST", "portfolio/unpublish"), routeContext("portfolio", "unpublish"));

    expect(response.status).toBe(502);
    expect(await response.json()).toEqual({
      code: "PUBLIC_PORTFOLIO_CACHE_CONTEXT_INVALID",
      message: "Respons portofolio dari layanan tidak valid; perubahan belum dikirim.",
      mutationApplied: false,
    });
    expect(events).toEqual(["GET /api/portfolio"]);
  });

  test("reports an applied update when its published post-state has no server-owned slug", async () => {
    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);

      if (init?.method === "GET" && url.pathname === "/api/portfolio") {
        return Response.json({ ...privatePortfolio("unused"), slug: null });
      }

      return new Response(null, { status: 204 });
    }) as typeof fetch;

    const response = await PUT(request("PUT", "portfolio"), routeContext("portfolio"));

    expect(response.status).toBe(502);
    expect(await response.json()).toEqual({
      code: "PUBLIC_PORTFOLIO_CACHE_CONTEXT_INVALID",
      message: "Perubahan portofolio sudah tersimpan, tetapi status publikasinya tidak dapat diverifikasi. Coba lagi.",
      mutationApplied: true,
    });
    expect(events).toEqual(["PUT /api/portfolio", "GET /api/portfolio"]);
  });

  test("reports a successful publish whose upstream response omits the slug needed for expiration", async () => {
    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);
      return Response.json({});
    }) as typeof fetch;

    const response = await POST(request("POST", "portfolio/publish"), routeContext("portfolio", "publish"));

    expect(response.status).toBe(502);
    expect(await response.json()).toEqual({
      code: "PUBLIC_PORTFOLIO_CACHE_INVALIDATION_FAILED",
      message: "Portofolio sudah dipublikasikan, tetapi slug cache tidak diterima. Coba lagi.",
      mutationApplied: true,
    });
    expect(events).toEqual(["POST /api/portfolio/publish"]);
  });

  test("reports an applied unpublish when immediate expiration cannot be scheduled synchronously", async () => {
    const slug = "budi-santoso-rpl-2026";
    revalidateTagMock.mockImplementationOnce((tag: string, profile: { expire: number }) => {
      events.push(`REVALIDATE ${tag} ${JSON.stringify(profile)}`);
      throw new Error("cache unavailable");
    });

    globalThis.fetch = mock(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      events.push(`${init?.method} ${url.pathname}`);

      if (init?.method === "GET" && url.pathname === "/api/portfolio") {
        return Response.json(privatePortfolio(slug));
      }

      return new Response(null, { status: 204 });
    }) as typeof fetch;

    const response = await POST(request("POST", "portfolio/unpublish"), routeContext("portfolio", "unpublish"));

    expect(response.status).toBe(502);
    expect(await response.json()).toEqual({
      code: "PUBLIC_PORTFOLIO_CACHE_INVALIDATION_FAILED",
      message: "Publikasi sudah dinonaktifkan, tetapi invalidasi cache tidak berhasil dijadwalkan. Coba lagi.",
      mutationApplied: true,
    });
    expect(events).toEqual([
      "GET /api/portfolio",
      "POST /api/portfolio/unpublish",
      `REVALIDATE ${publicPortfolioCacheTag(slug)} {"expire":0}`,
    ]);
  });
});
