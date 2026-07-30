import { describe, expect, test } from "bun:test";
import { resolveGuardDecision, SEGMENT_ROLES } from "./guard";
import { roleHome } from "./roleHome";
import type { Role, Session } from "./session";

const ALL_ROLES: Role[] = [
  "SuperAdmin",
  "TenantAdmin",
  "DeptHead",
  "Teacher",
  "IndustryMentor",
  "Student",
  "ParentViewer",
];

const PROTECTED_SEGMENTS = Object.keys(SEGMENT_ROLES); // ["/sa", "/app", "/mentor", "/student"]

function sessionOf(role: Role): Session {
  return { id: "u-1", name: "Test User", role, tenantId: role === "SuperAdmin" || role === "IndustryMentor" ? null : "t-1" };
}

describe("resolveGuardDecision — AC VOK-H2-E2: matrix 4 role x 5 segment", () => {
  test("tanpa session -> redirect /login di semua segment terproteksi", () => {
    for (const segment of PROTECTED_SEGMENTS) {
      const decision = resolveGuardDecision(segment, null);
      expect(decision.type).toBe("redirect");
      if (decision.type === "redirect") {
        expect(decision.to.startsWith("/login")).toBe(true);
      }
    }
  });

  test("redirect belum-login menyertakan next= (path asal) utk kembali setelah login", () => {
    const decision = resolveGuardDecision("/app/siswa", null);
    expect(decision.type).toBe("redirect");
    if (decision.type === "redirect") {
      expect(decision.to).toContain("next=%2Fapp%2Fsiswa");
    }
  });

  test("matrix lengkap: tiap role x tiap segment terproteksi", () => {
    for (const role of ALL_ROLES) {
      for (const segment of PROTECTED_SEGMENTS) {
        const decision = resolveGuardDecision(segment, sessionOf(role));
        const shouldAllow = SEGMENT_ROLES[segment].includes(role);

        if (shouldAllow) {
          expect(decision).toEqual({ type: "allow" });
        } else {
          expect(decision.type).toBe("redirect");
          if (decision.type === "redirect") {
            expect(decision.to).toBe(roleHome(role));
          }
        }
      }
    }
  });

  test("subpath ikut aturan segment induk (mis. /app/siswa, /student/history)", () => {
    expect(resolveGuardDecision("/app/siswa", sessionOf("Teacher"))).toEqual({ type: "allow" });
    expect(resolveGuardDecision("/student/history", sessionOf("Student"))).toEqual({ type: "allow" });
    expect(resolveGuardDecision("/mentor/nilai", sessionOf("Student")).type).toBe("redirect");
  });

  test("/, /login, /p/*, /verify/* selalu publik — dgn atau tanpa session", () => {
    const paths = ["/", "/login", "/p/smk-1-slug", "/verify/ABC123XYZ"];
    for (const p of paths) {
      expect(resolveGuardDecision(p, null)).toEqual({ type: "allow" });
      expect(resolveGuardDecision(p, sessionOf("Student"))).toEqual({ type: "allow" });
    }
  });

  test("path di luar 4 segment terproteksi (mis. /api/*) dibiarkan lolos — bukan tanggung jawab guard ini", () => {
    expect(resolveGuardDecision("/api/proxy/students", null)).toEqual({ type: "allow" });
  });

  test("invariant anti-loop: roleHome(role) — bila mengarah ke segment terproteksi, role itu wajib diizinkan di sana", () => {
    for (const role of ALL_ROLES) {
      const home = roleHome(role);
      if (home === "/login") continue; // ParentViewer dst — tanpa dashboard, aman (halaman publik).
      expect(SEGMENT_ROLES[home].includes(role)).toBe(true);
    }
  });
});
