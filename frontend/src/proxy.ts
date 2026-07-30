import { NextResponse, type NextRequest } from "next/server";
import { resolveGuardDecision } from "@/lib/guard";
import { getSessionEdge } from "@/lib/session";

/**
 * VOK-H2-E2 §proxy.ts — guard matrix segment×role.
 *
 * [RUNNER NOTE] Ticket menulis "proxy.ts (root frontend)" dgn signature "middleware(req) ->
 * NextResponse" — istilah lama. Next.js 16 (frontend/package.json: next 16.2.10) mengganti nama
 * konvensi file+fungsi middleware -> proxy (nextjs.org/docs/app/api-reference/file-conventions/
 * proxy, "Migration to Proxy"); nama fungsi export WAJIB persis "proxy" (atau default export)
 * agar dikenali build. Lokasi juga dikoreksi ke src/proxy.ts (bukan frontend/proxy.ts) krn app/
 * project ini ada di src/app — dok Next mewajibkan proxy.ts sejajar dgn app/. Dicatat eksplisit
 * (SOUL.md: dilarang menyimpang diam-diam), bukan perubahan logika — DECISIONS.md D16.
 */
export async function proxy(request: NextRequest): Promise<NextResponse> {
  const session = await getSessionEdge(request);
  const decision = resolveGuardDecision(request.nextUrl.pathname, session);

  if (decision.type === "redirect") {
    return NextResponse.redirect(new URL(decision.to, request.url));
  }

  return NextResponse.next();
}

// Hanya jalan di 4 segment terproteksi (":path*" = termasuk path segment itu sendiri, bukan cuma
// anak-anaknya) — di luar itu (landing, /login, /p/*, /verify/*, /api/*, aset statis) tidak disentuh.
export const config = {
  matcher: ["/sa/:path*", "/app/:path*", "/mentor/:path*", "/student/:path*"],
};
