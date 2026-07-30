import { describe, expect, test } from "bun:test";
import { decodeSessionCookie, encodeSessionCookie, getSessionEdge, SESSION_COOKIE, type Session } from "./session";

const sample: Session = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "Budi Santoso",
  role: "Teacher",
  tenantId: "t-1",
};

describe("session cookie encode/decode — kontrak lite tanpa-DB (VOK-H2-E2)", () => {
  test("round-trip encode -> decode menghasilkan objek identik", async () => {
    expect(await decodeSessionCookie(await encodeSessionCookie(sample))).toEqual(sample);
  });

  test("cookie kosong/undefined -> null (bukan crash)", async () => {
    expect(await decodeSessionCookie(undefined)).toBeNull();
    expect(await decodeSessionCookie(null)).toBeNull();
    expect(await decodeSessionCookie("")).toBeNull();
  });

  test("cookie rusak/dipalsu asal -> null, tidak melempar exception", async () => {
    expect(await decodeSessionCookie("bukan-base64-json-valid!!!")).toBeNull();
    expect(await decodeSessionCookie(Buffer.from("{not valid json", "utf-8").toString("base64url"))).toBeNull();
  });

  test("cookie tanpa field wajib (id/role) -> null", async () => {
    const bad = Buffer.from(JSON.stringify({ name: "Tanpa Id Atau Role" }), "utf-8").toString("base64url");
    expect(await decodeSessionCookie(`${bad}.invalid`)).toBeNull();
  });

  test("tenantId null (mis. IndustryMentor lintas-tenant) dipertahankan, bukan hilang", async () => {
    const mentor: Session = { id: "u-2", name: "Mentor DUDI", role: "IndustryMentor", tenantId: null };
    expect((await decodeSessionCookie(await encodeSessionCookie(mentor)))?.tenantId).toBeNull();
  });

  test("getSessionEdge baca dari objek mirip-NextRequest (mock cookie) tanpa panggilan DB", async () => {
    const fakeReq = {
      cookies: {
        get: (name: string) => (name === SESSION_COOKIE ? { value: "" } : undefined),
      },
    };
    fakeReq.cookies.get = (name: string) => (name === SESSION_COOKIE ? { value: "pending" } : undefined);
    const cookie = await encodeSessionCookie(sample);
    fakeReq.cookies.get = (name: string) => (name === SESSION_COOKIE ? { value: cookie } : undefined);
    expect(await getSessionEdge(fakeReq)).toEqual(sample);
  });

  test("getSessionEdge tanpa cookie sama sekali -> null", async () => {
    const fakeReq = { cookies: { get: () => undefined } };
    expect(await getSessionEdge(fakeReq)).toBeNull();
  });
});
