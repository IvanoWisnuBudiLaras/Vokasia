import { expect, test } from "bun:test";

import { buildSecurityHeaders } from "./securityHeaders";

test("public Next.js responses receive the complete baseline security policy", () => {
  const headers = new Map(
    buildSecurityHeaders(true).map(({ key, value }) => [key, value])
  );

  expect(headers.get("Content-Security-Policy")).toContain("default-src 'self'");
  expect(headers.get("Content-Security-Policy")).toContain("frame-ancestors 'none'");
  expect(headers.get("Content-Security-Policy")).toContain("object-src 'none'");
  expect(headers.get("Content-Security-Policy")).toContain(
    "connect-src 'self' https://api.iconify.design https://api.simplesvg.com https://api.unisvg.com"
  );
  expect(headers.get("Permissions-Policy")).toBe(
    "camera=(), microphone=(), geolocation=(), browsing-topics=()"
  );
  expect(headers.get("X-Content-Type-Options")).toBe("nosniff");
  expect(headers.get("X-Frame-Options")).toBe("DENY");
  expect(headers.get("Referrer-Policy")).toBe("no-referrer");
  expect(headers.get("Strict-Transport-Security")).toBe(
    "max-age=31536000; includeSubDomains"
  );
});

test("development CSP permits eval only for the Next.js development runtime", () => {
  const productionCsp = buildSecurityHeaders(true).find(
    ({ key }) => key === "Content-Security-Policy"
  )?.value;
  const developmentCsp = buildSecurityHeaders(false).find(
    ({ key }) => key === "Content-Security-Policy"
  )?.value;

  expect(productionCsp).not.toContain("'unsafe-eval'");
  expect(productionCsp).not.toContain(" http:");
  expect(developmentCsp).toContain("'unsafe-eval'");
  expect(buildSecurityHeaders(false).some(({ key }) => key === "Strict-Transport-Security")).toBe(
    false
  );
});

test("configured browser storage origin is limited to image and connection directives", () => {
  const csp = buildSecurityHeaders(true, "http://localhost:9000").find(
    ({ key }) => key === "Content-Security-Policy"
  )?.value;

  expect(csp).toContain("img-src 'self' blob: data: http://localhost:9000");
  expect(csp).toContain(
    "connect-src 'self' https://api.iconify.design https://api.simplesvg.com https://api.unisvg.com http://localhost:9000"
  );
  expect(csp).not.toContain("script-src 'self' 'unsafe-inline' http://localhost:9000");
});
