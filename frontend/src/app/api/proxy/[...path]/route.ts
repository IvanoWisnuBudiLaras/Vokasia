import { revalidatePath, revalidateTag } from "next/cache";
import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { decodeSessCookie, getSessionData, SESS_COOKIE_NAME } from "@/lib/bffSession";
import { publicPortfolioCacheTag } from "@/lib/publicPortfolioCache";
import { refreshOnExpiry } from "@/lib/refresh";
import { SESSION_COOKIE } from "@/lib/session";

function apiBase(): string {
  return process.env.API_INTERNAL_URL ?? "http://localhost:5000";
}

interface RouteContext {
  params: Promise<{ path: string[] }>;
}

type PortfolioMutation = "publish" | "unpublish" | "update" | null;

interface PrivatePortfolioState {
  isPublished: boolean;
  slug: string | null;
}

function portfolioMutation(method: string, path: string[]): PortfolioMutation {
  if (method === "PUT" && path.length === 1 && path[0] === "portfolio") {
    return "update";
  }

  if (method === "POST" && path.length === 2 && path[0] === "portfolio") {
    if (path[1] === "publish") return "publish";
    if (path[1] === "unpublish") return "unpublish";
  }

  return null;
}

function readPrivatePortfolioState(value: unknown): PrivatePortfolioState | null {
  if (!value || typeof value !== "object") return null;

  const candidate = value as Record<string, unknown>;
  if (typeof candidate.isPublished !== "boolean") return null;
  if (candidate.slug !== null && typeof candidate.slug !== "string") return null;

  return { isPublished: candidate.isPublished, slug: candidate.slug };
}

function readPublishedSlug(body: ArrayBuffer): string | null {
  try {
    const value = JSON.parse(new TextDecoder().decode(body)) as unknown;
    if (!value || typeof value !== "object") return null;

    const slug = (value as Record<string, unknown>).slug;
    return typeof slug === "string" && slug.length > 0 ? slug : null;
  } catch {
    return null;
  }
}

async function forward(upstream: Response): Promise<NextResponse> {
  if (upstream.status === 204 || upstream.status === 205 || upstream.status === 304) {
    return new NextResponse(null, { status: upstream.status });
  }
  const body = await upstream.arrayBuffer();
  return new NextResponse(body, {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
}

/**
 * VOK-H2-E3 proxyWithBearer — satu-satunya jalur FE->API (fetcher.ts panggil `/api/proxy/*`):
 * ambil access token dari Redis (BUKAN dari client), tempelkan Authorization: Bearer, teruskan;
 * 401 -> refreshOnExpiry 1x -> ulang sekali. Body request dibaca SEKALI di awal (Request body
 * adalah stream sekali-baca) supaya bisa dipakai ulang persis di percobaan kedua.
 */
async function handle(req: NextRequest, ctx: RouteContext): Promise<NextResponse> {
  const store = await cookies();
  const sessionId = decodeSessCookie(store.get(SESS_COOKIE_NAME)?.value);
  if (!sessionId) {
    const res = NextResponse.json({ message: "Belum login." }, { status: 401 });
    res.cookies.delete(SESS_COOKIE_NAME);
    res.cookies.delete(SESSION_COOKIE);
    return res;
  }

  const session = await getSessionData(sessionId);
  if (!session) {
    const res = NextResponse.json({ message: "Sesi tidak ditemukan atau kedaluwarsa." }, { status: 401 });
    res.cookies.delete(SESS_COOKIE_NAME);
    res.cookies.delete(SESSION_COOKIE);
    return res;
  }

  const { path } = await ctx.params;
  const rawPath = path.join("/");
  const targetPath = rawPath.startsWith("sa") ? `/${rawPath}` : `/api/${rawPath}`;
  const upstreamUrl = new URL(`${targetPath}${req.nextUrl.search}`, apiBase());
  const contentType = req.headers.get("content-type");
  const hasBody = !["GET", "HEAD"].includes(req.method);
  const bodyBuffer = hasBody ? await req.arrayBuffer() : undefined;
  let accessToken = session.accessToken;

  const fetchWithBearer = async (url: URL, init: RequestInit): Promise<Response> => {
    const doFetch = (token: string) => {
      const headers = new Headers(init.headers);
      headers.set("Authorization", `Bearer ${token}`);
      return fetch(url, { ...init, headers, cache: "no-store" });
    };

    let upstream = await doFetch(accessToken);

    if (upstream.status === 401) {
      const refreshed = await refreshOnExpiry(sessionId);
      if (refreshed.ok) {
        accessToken = refreshed.accessToken;
        upstream = await doFetch(accessToken);
      }
    }

    return upstream;
  };

  const mutation = portfolioMutation(req.method, path);
  let slugToExpire: string | null = null;

  // Unpublish tidak mengembalikan slug. State privat dibaca dengan bearer token BFF sebelum
  // mutation, sehingga browser tidak pernah memilih cache tag yang akan dicabut.
  if (mutation === "unpublish") {
    const portfolioResponse = await fetchWithBearer(new URL("/api/portfolio", apiBase()), { method: "GET" });
    if (!portfolioResponse.ok) {
      return forward(portfolioResponse);
    }

    const privateState = readPrivatePortfolioState(await portfolioResponse.json().catch(() => null));
    if (!privateState) {
      return NextResponse.json(
        {
          code: "PUBLIC_PORTFOLIO_CACHE_CONTEXT_INVALID",
          message: "Respons portofolio dari layanan tidak valid; perubahan belum dikirim.",
          mutationApplied: false,
        },
        { status: 502 }
      );
    }

    if (privateState.isPublished && !privateState.slug) {
      return NextResponse.json(
        {
          code: "PUBLIC_PORTFOLIO_CACHE_CONTEXT_INVALID",
          message: "Portofolio aktif tidak memiliki slug cache; perubahan belum dikirim.",
          mutationApplied: false,
        },
        { status: 502 }
      );
    }

    slugToExpire = privateState.slug;
  }

  const upstream = await fetchWithBearer(upstreamUrl, {
    method: req.method,
    headers: contentType ? { "Content-Type": contentType } : undefined,
    body: bodyBuffer,
  });
  const outBody = await upstream.arrayBuffer();

  if (upstream.ok && mutation) {
    if (mutation === "publish") {
      slugToExpire = readPublishedSlug(outBody);
      if (!slugToExpire) {
        return NextResponse.json(
          {
            code: "PUBLIC_PORTFOLIO_CACHE_INVALIDATION_FAILED",
            message: "Portofolio sudah dipublikasikan, tetapi slug cache tidak diterima. Coba lagi.",
            mutationApplied: true,
          },
          { status: 502 }
        );
      }
    }

    if (mutation === "update") {
      const portfolioResponse = await fetchWithBearer(new URL("/api/portfolio", apiBase()), { method: "GET" });
      const privateState = portfolioResponse.ok
        ? readPrivatePortfolioState(await portfolioResponse.json().catch(() => null))
        : null;

      if (!privateState || (privateState.isPublished && !privateState.slug)) {
        return NextResponse.json(
          {
            code: "PUBLIC_PORTFOLIO_CACHE_CONTEXT_INVALID",
            message: "Perubahan portofolio sudah tersimpan, tetapi status publikasinya tidak dapat diverifikasi. Coba lagi.",
            mutationApplied: true,
          },
          { status: 502 }
        );
      }

      slugToExpire = privateState.isPublished ? privateState.slug : null;
    }

    if (slugToExpire) {
      // Next.js 16 hanya mengekspos API void: pemanggilan ini menjadwalkan tag ke pendingWaitUntil.
      // Karena hasil purge storage tidak awaitable, catch hanya bisa melaporkan kegagalan
      // penjadwalan sinkron; kegagalan cache-handler asinkron berada di luar respons Route Handler.
      try {
        revalidateTag(publicPortfolioCacheTag(slugToExpire), { expire: 0 });
        revalidatePath(`/p/${slugToExpire}`);
      } catch {
        const message =
          mutation === "unpublish"
            ? "Publikasi sudah dinonaktifkan, tetapi invalidasi cache tidak berhasil dijadwalkan. Coba lagi."
            : "Perubahan portofolio sudah tersimpan, tetapi invalidasi cache tidak berhasil dijadwalkan. Coba lagi.";

        return NextResponse.json(
          {
            code: "PUBLIC_PORTFOLIO_CACHE_INVALIDATION_FAILED",
            message,
            mutationApplied: true,
          },
          { status: 502 }
        );
      }
    }
  }

  if (upstream.status === 204 || upstream.status === 205 || upstream.status === 304) {
    return new NextResponse(null, { status: upstream.status });
  }

  const response = new NextResponse(outBody, {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });

  if (upstream.status === 401) {
    response.cookies.delete(SESS_COOKIE_NAME);
    response.cookies.delete(SESSION_COOKIE);
  }

  return response;
}

export const GET = handle;
export const POST = handle;
export const PUT = handle;
export const PATCH = handle;
export const DELETE = handle;
