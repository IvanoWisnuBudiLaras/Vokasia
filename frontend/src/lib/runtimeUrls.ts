/** Browser-visible origins must never silently fall back to localhost in a production image. */
export function getRuntimeUrl(name: "API_PUBLIC_URL" | "NEXT_PUBLIC_APP_URL", developmentFallback: string): string {
  const value = process.env[name];
  if (value) return value.replace(/\/$/, "");
  if (process.env.NODE_ENV === "production") {
    throw new Error(`${name} wajib dikonfigurasi di production.`);
  }
  return developmentFallback;
}

export function getOidcClientSecret(): string {
  const value = process.env.OIDC_BFF_CLIENT_SECRET;
  if (value) return value;
  if (process.env.NODE_ENV === "production") {
    throw new Error("OIDC_BFF_CLIENT_SECRET wajib dikonfigurasi di production.");
  }
  return "dev-only-secret-change-me";
}
