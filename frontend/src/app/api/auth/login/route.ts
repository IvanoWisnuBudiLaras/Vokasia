import { NextResponse } from "next/server";
import { savePkce } from "@/lib/bffSession";
import { getSafeLocalReturnUrl } from "@/lib/localReturnUrl";
import { generatePkce, generateState } from "@/lib/pkce";
import { getRuntimeUrl } from "@/lib/runtimeUrls";

const BFF_CLIENT_ID = "vokasia-bff";
// [RUNNER NOTE] "profile" SENGAJA di-drop dari scope yg diminta (beda dari draf awal) — client
// diberi permission scp:profile sejak H1-E3 tapi scope "profile" itu sendiri tak pernah
// didaftarkan ke OpenIddictScopes (baru ketahuan lewat smoke test HTTP nyata: 400 invalid_scope).
// "profile" juga tidak dipakai apa pun di sini (klaim sub/role/tenant_id/name sudah otomatis
// masuk access token via VokasiaClaimsFactory, tidak digerbangi scope) — daripada menambah
// registrasi scope yang tak dipakai, cukup jangan diminta. "api" DIDAFTARKAN (OpenIddictSetup.cs
// SeedOAuthClientsAsync) krn representasi resource server yang nyata dipakai proxyWithBearer.
const SCOPE = "api offline_access";

/**
 * VOK-H2-E3 handleLogin — mulai code+PKCE: generate verifier+challenge+state, simpan di Redis, redirect /connect/authorize.
 *
 * BUGFIX VOK-H4-E2 live-verification (DECISIONS.md D26 -> D28, sebelumnya diketahui tp dibiarkan
 * "out of scope" di D26): URL di bawah ini dikirim via NextResponse.redirect() ke BROWSER (bukan
 * fetch server-to-server spt callback/route.ts) — jadi HARUS pakai host yg bisa di-resolve browser
 * (API_PUBLIC_URL, publish port host "localhost:5000"), BUKAN API_INTERNAL_URL (di docker-compose
 * = "http://api:8080", Docker-internal DNS, browser di host tak bisa resolve -> hard nav error).
 * Ketahuan lewat live browser E2E test thd docker-compose stack (bukan lewat `bun run dev` +
 * `dotnet run` langsung di host, krn di mode itu API_INTERNAL_URL DAN API_PUBLIC_URL sama2
 * "localhost:5000" - bug ini cuma nongol saat frontend jalan DI DALAM container).
 */
export async function GET(req: Request) {
  const url = new URL(req.url);
  const next = getSafeLocalReturnUrl(url.searchParams.get("next")) ?? "";

  const { verifier, challenge } = generatePkce();
  const state = generateState();
  await savePkce(state, JSON.stringify({ verifier, next }));

  const apiPublicBase = getRuntimeUrl("API_PUBLIC_URL", "http://localhost:5000");
  const appUrl = getRuntimeUrl("NEXT_PUBLIC_APP_URL", "http://localhost:3000");

  const authorizeUrl = new URL("/connect/authorize", apiPublicBase);
  authorizeUrl.searchParams.set("client_id", BFF_CLIENT_ID);
  authorizeUrl.searchParams.set("response_type", "code");
  authorizeUrl.searchParams.set("redirect_uri", `${appUrl}/api/auth/callback`);
  authorizeUrl.searchParams.set("scope", SCOPE);
  authorizeUrl.searchParams.set("state", state);
  authorizeUrl.searchParams.set("code_challenge", challenge);
  authorizeUrl.searchParams.set("code_challenge_method", "S256");

  return NextResponse.redirect(authorizeUrl);
}
