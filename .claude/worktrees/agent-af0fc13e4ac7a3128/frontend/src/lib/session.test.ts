import { describe, expect, test } from "bun:test";
import { decodeSessionCookie, encodeSessionCookie, getSessionEdge, SESSION_COOKIE, type Session } from "./session";

const sample: Session = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "Budi Santoso",
  role: "Teacher",
  tenantId: "t-1",
};

describe("session cookie encode/decode — kontrak lite tanpa-DB (VOK-H2-E2)", () => {
  test("round-trip encode -> decode menghasilkan objek identik", () => {
    expect(decodeSessionCookie(encodeSessionCookie(sample))).toEqual(sample);
  });

  test("cookie kosong/undefined -> null (bukan crash)", () => {
    expect(decodeSessionCookie(undefined)).toBeNull();
    expect(decodeSessionCookie(null)).toBeNull();
    expect(decodeSessionCookie("")).toBeNull();
  });

  test("cookie rusak/dipalsu asal -> null, tidak melempar exception", () => {
    expect(decodeSessionCookie("bukan-base64-json-valid!!!")).toBeNull();
    expect(decodeSessionCookie(Buffer.from("{not valid json", "utf-8").toString("base64url"))).toBeNull();
  });

  test("cookie tanpa field wajib (id/role) -> null", () => {
    const bad = Buffer.from(JSON.stringify({ name: "Tanpa Id Atau Role" }), "utf-8").toString("base64url");
    expect(decodeSessionCookie(bad)).toBeNull();
  });

  test("tenantId null (mis. IndustryMentor lintas-tenant) dipertahankan, bukan hilang", () => {
    const mentor: Session = { id: "u-2", name: "Mentor DUDI", role: "IndustryMentor", tenantId: null };
    expect(decodeSessionCookie(encodeSessionCookie(mentor))?.tenantId).toBeNull();
  });

  test("getSessionEdge baca dari objek mirip-NextRequest (mock cookie) tanpa panggilan DB", () => {
    const fakeReq = {
      cookies: {
        get: (name: string) => (name === SESSION_COOKIE ? { value: encodeSessionCookie(sample) } : undefined),
      },
    };
    expect(getSessionEdge(fakeReq)).toEqual(sample);
  });

  test("getSessionEdge tanpa cookie sama sekali -> null", () => {
    const fakeReq = { cookies: { get: () => undefined } };
    expect(getSessionEdge(fakeReq)).toBeNull();
  });
});
