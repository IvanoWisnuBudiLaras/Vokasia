import { afterEach, expect, test } from "bun:test";
import { getOidcClientSecret, getRuntimeUrl } from "./runtimeUrls";

const originalNodeEnv = process.env.NODE_ENV;
const originalApiUrl = process.env.API_PUBLIC_URL;
const originalOidcSecret = process.env.OIDC_BFF_CLIENT_SECRET;

afterEach(() => {
  process.env.NODE_ENV = originalNodeEnv;
  process.env.API_PUBLIC_URL = originalApiUrl;
  process.env.OIDC_BFF_CLIENT_SECRET = originalOidcSecret;
});

test("runtime public URL uses a development fallback only outside production", () => {
  process.env.NODE_ENV = "development";
  delete process.env.API_PUBLIC_URL;
  expect(getRuntimeUrl("API_PUBLIC_URL", "http://localhost:5000")).toBe("http://localhost:5000");
});

test("runtime public URL fails fast in production when missing", () => {
  process.env.NODE_ENV = "production";
  delete process.env.API_PUBLIC_URL;
  expect(() => getRuntimeUrl("API_PUBLIC_URL", "http://localhost:5000")).toThrow("API_PUBLIC_URL");
});

test("OIDC client secret has no production fallback", () => {
  process.env.NODE_ENV = "production";
  delete process.env.OIDC_BFF_CLIENT_SECRET;
  expect(() => getOidcClientSecret()).toThrow("OIDC_BFF_CLIENT_SECRET");
});
