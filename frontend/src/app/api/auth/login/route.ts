import { NextResponse } from "next/server";
import { savePkce } from "@/lib/bffSession";
import { generatePkce, generateState } from "@/lib/pkce";

const BFF_CLIENT_ID = "vokasia-bff";
// [RUNNER NOTE] "profile" SENGAJA di-drop dari scope yg diminta (beda dari draf awal) — client
// diberi permission scp:profile sejak H1-E3 tapi scope "profile" itu sendiri tak pernah
// didaftarkan ke OpenIddictScopes (baru ketahuan lewat smoke test HTTP nyata: 400 invalid_scope).
// "profile" juga tidak dipakai apa pun di sini (klaim sub/role/tenant_id/name sudah otomatis
// masuk access token via VokasiaClaimsFactory, tidak digerbangi scope) — daripada menambah
// registrasi scope yang tak dipakai, cukup jangan diminta. "api" DIDAFTARKAN (OpenIddictSetup.cs
// SeedOAuthClientsAsync) krn representasi resource server yang nyata dipakai proxyWithBearer.
const SCOPE = "api offline_access";

/** VOK-H2-E3 handleLogin — mulai code+PKCE: generate verifier+challenge+state, simpan di Redis, redirect /connect/authorize. */
export async function GET(req: Request) {
  const url = new URL(req.url);
  const next = url.searchParams.get("next") ?? "";

  const { verifier, challenge } = generatePkce();
  const state = generateState();
  await savePkce(state, JSON.stringify({ verifier, next }));

  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const appUrl = process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000";

  const authorizeUrl = new URL("/connect/authorize", apiBase);
  authorizeUrl.searchParams.set("client_id", BFF_CLIENT_ID);
  authorizeUrl.searchParams.set("response_type", "code");
  authorizeUrl.searchParams.set("redirect_uri", `${appUrl}/api/auth/callback`);
  authorizeUrl.searchParams.set("scope", SCOPE);
  authorizeUrl.searchParams.set("state", state);
  authorizeUrl.searchParams.set("code_challenge", challenge);
  authorizeUrl.searchParams.set("code_challenge_method", "S256");

  return NextResponse.redirect(authorizeUrl);
}
