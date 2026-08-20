export interface SecurityHeader {
  key: string;
  value: string;
}

/**
 * Baseline browser policy for the standalone Next.js origin.
 *
 * A nonce would force every page to render dynamically. The public landing/offline pages need to
 * remain static, so this follows Next.js' documented non-nonce policy and permits only the inline
 * script/style execution required by the framework. The http/https allowances are intentional:
 * browser uploads and public thumbnails use runtime presigned MinIO URLs whose host is selected by
 * the backend and cannot safely be hard-coded into the frontend build.
 */
export function buildSecurityHeaders(isProduction: boolean, storageOrigin?: string): SecurityHeader[] {
  const directBrowserOrigins = storageOrigin ? ` ${storageOrigin.replace(/\/$/, "")}` : "";
  const iconifyApiOrigins = " https://api.iconify.design https://api.simplesvg.com https://api.unisvg.com";
  const contentSecurityPolicy = [
    "default-src 'self'",
    `script-src 'self' 'unsafe-inline'${isProduction ? "" : " 'unsafe-eval'"}`,
    "style-src 'self' 'unsafe-inline'",
    `img-src 'self' blob: data:${directBrowserOrigins}`,
    "font-src 'self' data:",
    `connect-src 'self'${iconifyApiOrigins}${directBrowserOrigins}${!isProduction ? " http://localhost:9000" : ""}`,
    "worker-src 'self' blob:",
    "manifest-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join("; ");

  const headers: SecurityHeader[] = [
    { key: "Content-Security-Policy", value: contentSecurityPolicy },
    {
      key: "Permissions-Policy",
      value: "camera=(), microphone=(), geolocation=(), browsing-topics=()",
    },
    { key: "X-Content-Type-Options", value: "nosniff" },
    { key: "X-Frame-Options", value: "DENY" },
    { key: "Referrer-Policy", value: "no-referrer" },
  ];

  if (isProduction) {
    headers.push({
      key: "Strict-Transport-Security",
      value: "max-age=31536000; includeSubDomains",
    });
  }

  return headers;
}
