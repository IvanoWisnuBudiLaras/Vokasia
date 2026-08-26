import { NextResponse, type NextRequest } from "next/server";
import { resolveGuardDecision } from "@/lib/guard";
import { getSessionEdge } from "@/lib/session";

/**
 * Next.js Middleware - Route Guard Matrix (segment x role).
 * Menjaga segment terproteksi agar tidak bisa diakses tanpa cookie sesi yang valid.
 */
export async function proxy(request: NextRequest): Promise<NextResponse> {
  if (request.nextUrl.pathname.startsWith("/p/")) {
    const slug = request.nextUrl.pathname.slice("/p/".length);
    const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
    try {
      const response = await fetch(`${apiBase}/p/${encodeURIComponent(slug)}`, { cache: "no-store" });
      if (response.status === 404) {
        return NextResponse.rewrite(new URL("/_not-found", request.url), { status: 404 });
      }
    } catch {
      // Fallback
    }
  }

  const session = await getSessionEdge(request);
  const decision = resolveGuardDecision(request.nextUrl.pathname, session);

  if (decision.type === "redirect") {
    return NextResponse.redirect(new URL(decision.to, request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/sa/:path*", "/app/:path*", "/mentor/:path*", "/student/:path*", "/p/:slug"],
};
