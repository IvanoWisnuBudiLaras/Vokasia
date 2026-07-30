import {
  afterAll,
  beforeAll,
  beforeEach,
  describe,
  expect,
  mock,
  spyOn,
  test,
} from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import * as bffSession from "@/lib/bffSession";
import * as pkce from "@/lib/pkce";
import * as session from "@/lib/session";
import LoginPage from "../../login/page";
import { GET as finishLogin } from "./callback/route";
import { GET as startLogin } from "./login/route";

let savedPkcePayload = "";
let storedNext = "";

const originalFetch = globalThis.fetch;
const originalAppUrl = process.env.NEXT_PUBLIC_APP_URL;
const originalApiPublicUrl = process.env.API_PUBLIC_URL;
const originalApiInternalUrl = process.env.API_INTERNAL_URL;
const originalSessionSecret = process.env.SESSION_SECRET;

beforeAll(() => {
  process.env.NEXT_PUBLIC_APP_URL = "http://app.test";
  process.env.API_PUBLIC_URL = "http://api.test";
  process.env.API_INTERNAL_URL = "http://api.test";
  process.env.SESSION_SECRET = "test-session-secret-only";

  globalThis.fetch = mock(async () =>
    Response.json({
      access_token:
        "header." +
        Buffer.from(
          JSON.stringify({
            sub: "student-id",
            name: "Siswa Contoh",
            role: "Student",
            tenant_id: "tenant-id",
          }),
          "utf8",
        ).toString("base64url") +
        ".signature",
      refresh_token: "refresh-token",
      expires_in: 900,
    }),
  ) as typeof fetch;

  spyOn(bffSession, "savePkce").mockImplementation(
    async (_state: string, payload: string) => {
      savedPkcePayload = payload;
    },
  );
  spyOn(bffSession, "consumePkce").mockImplementation(async () =>
    JSON.stringify({
      verifier: "test-verifier",
      next: storedNext,
    }),
  );
  spyOn(bffSession, "createSession").mockImplementation(
    async () => "test-session-id",
  );
  spyOn(pkce, "generatePkce").mockImplementation(() => ({
    verifier: "test-verifier",
    challenge: "test-challenge",
  }));
  spyOn(pkce, "generateState").mockImplementation(() => "test-state");
  spyOn(session, "getSession").mockImplementation(async () => null);
});

beforeEach(() => {
  savedPkcePayload = "";
  storedNext = "";
});

afterAll(() => {
  mock.restore();
  globalThis.fetch = originalFetch;

  if (originalAppUrl === undefined) {
    delete process.env.NEXT_PUBLIC_APP_URL;
  } else {
  process.env.NEXT_PUBLIC_APP_URL = originalAppUrl;
  process.env.API_PUBLIC_URL = originalApiPublicUrl;
  }

  if (originalApiInternalUrl === undefined) {
    delete process.env.API_INTERNAL_URL;
  } else {
    process.env.API_INTERNAL_URL = originalApiInternalUrl;
  }

  if (originalSessionSecret === undefined) {
    delete process.env.SESSION_SECRET;
  } else {
    process.env.SESSION_SECRET = originalSessionSecret;
  }
});

describe("GET /api/auth/login return destination", () => {
  test("does not persist an absolute external next URL", async () => {
    const next = "https://evil.example/steal";

    const response = await startLogin(
      new Request(
        "http://app.test/api/auth/login?next=" +
          encodeURIComponent(next),
      ),
    );

    expect(response.status).toBe(307);
    expect(JSON.parse(savedPkcePayload)).toEqual({
      verifier: "test-verifier",
      next: "",
    });
  });

  test("does not persist a protocol-relative next URL", async () => {
    const next = "//evil.example/steal";

    await startLogin(
      new Request(
        "http://app.test/api/auth/login?next=" +
          encodeURIComponent(next),
      ),
    );

    expect(JSON.parse(savedPkcePayload).next).toBe("");
  });

  test("does not persist a next URL containing a backslash", async () => {
    const next = String.raw`/\evil.example/steal`;

    await startLogin(
      new Request(
        "http://app.test/api/auth/login?next=" +
          encodeURIComponent(next),
      ),
    );

    expect(JSON.parse(savedPkcePayload).next).toBe("");
  });

  test("does not persist a percent-encoded control character", async () => {
    const next = "/student/history\nset-cookie";

    await startLogin(
      new Request(
        "http://app.test/api/auth/login?next=" +
          encodeURIComponent(next),
      ),
    );

    expect(JSON.parse(savedPkcePayload).next).toBe("");
  });

  test("does not persist a C1 control character", async () => {
    const next = "/student/history\u0085set-cookie";

    await startLogin(
      new Request(
        "http://app.test/api/auth/login?next=" +
          encodeURIComponent(next),
      ),
    );

    expect(JSON.parse(savedPkcePayload).next).toBe("");
  });

  test("preserves a local path with its query string", async () => {
    const next = "/student/history?tab=minggu%2Fini&from=dashboard";

    await startLogin(
      new Request(
        "http://app.test/api/auth/login?next=" +
          encodeURIComponent(next),
      ),
    );

    expect(JSON.parse(savedPkcePayload).next).toBe(next);
  });
});

describe("GET /api/auth/callback return destination", () => {
  test("revalidates a stored absolute URL before redirecting", async () => {
    storedNext = "https://evil.example/steal";

    const response = await finishLogin(
      new Request(
        "http://app.test/api/auth/callback?code=test-code&state=test-state",
      ),
    );

    expect(response.status).toBe(307);
    expect(response.headers.get("location")).toBe("http://app.test/student");
  });

  test("preserves a stored local path with its query string", async () => {
    storedNext = "/student/history?tab=minggu%2Fini&from=dashboard";

    const response = await finishLogin(
      new Request(
        "http://app.test/api/auth/callback?code=test-code&state=test-state",
      ),
    );

    expect(response.headers.get("location")).toBe(
      "http://app.test/student/history?tab=minggu%2Fini&from=dashboard",
    );
  });
});

describe("/login return destination", () => {
  test("does not forward an absolute external next URL to the BFF", async () => {
    const page = await LoginPage({
      searchParams: Promise.resolve({
        next: "https://evil.example/steal",
      }),
    });

    const html = renderToStaticMarkup(page);

    expect(html).toContain('href="/api/auth/login"');
    expect(html).not.toContain("evil.example");
  });

  test("forwards a local path and query to the BFF unchanged", async () => {
    const page = await LoginPage({
      searchParams: Promise.resolve({
        next: "/student/history?tab=minggu%2Fini&from=dashboard",
      }),
    });

    const html = renderToStaticMarkup(page);

    expect(html).toContain(
      'href="/api/auth/login?next=%2Fstudent%2Fhistory%3Ftab%3Dminggu%252Fini%26from%3Ddashboard"',
    );
  });
});
