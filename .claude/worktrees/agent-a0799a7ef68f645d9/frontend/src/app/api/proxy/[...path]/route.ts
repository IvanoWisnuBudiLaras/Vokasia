import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { decodeSessCookie, getSessionData, SESS_COOKIE_NAME } from "@/lib/bffSession";
import { refreshOnExpiry } from "@/lib/refresh";

function apiBase(): string {
  return process.env.API_INTERNAL_URL ?? "http://localhost:5000";
}

interface RouteContext {
  params: Promise<{ path: string[] }>;
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
    return NextResponse.json({ message: "Belum login." }, { status: 401 });
  }

  const session = await getSessionData(sessionId);
  if (!session) {
    return NextResponse.json({ message: "Sesi tidak ditemukan atau kedaluwarsa." }, { status: 401 });
  }

  const { path } = await ctx.params;
  const upstreamUrl = new URL(`/api/${path.join("/")}${req.nextUrl.search}`, apiBase());
  const contentType = req.headers.get("content-type");
  const hasBody = !["GET", "HEAD"].includes(req.method);
  const bodyBuffer = hasBody ? await req.arrayBuffer() : undefined;

  const doFetch = (accessToken: string) =>
    fetch(upstreamUrl, {
      method: req.method,
      headers: {
        Authorization: `Bearer ${accessToken}`,
        ...(contentType ? { "Content-Type": contentType } : {}),
      },
      body: bodyBuffer,
      cache: "no-store",
    });

  let upstream = await doFetch(session.accessToken);

  if (upstream.status === 401) {
    const refreshed = await refreshOnExpiry(sessionId);
    if (refreshed.ok) {
      upstream = await doFetch(refreshed.accessToken);
    }
  }

  const outBody = await upstream.arrayBuffer();
  return new NextResponse(outBody, {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
}

export const GET = handle;
export const POST = handle;
export const PUT = handle;
export const PATCH = handle;
export const DELETE = handle;
