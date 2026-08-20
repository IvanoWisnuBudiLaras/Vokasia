import { expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { OperationsWorkspace } from "./OperationsWorkspace";

test("TenantAdmin operations workspace uses human-readable workflow controls", () => {
  const html = renderToStaticMarkup(<OperationsWorkspace periods={[{ id: "period-1", name: "PKL 2026", status: "Active", startDate: "2026-01-01" }]} students={[]} staff={[{ id: "teacher-1", email: "teacher@example.test", fullName: "Guru Contoh", role: 3, isActive: true }]} companies={[{ id: "company-1", name: "PT Contoh", sector: null, city: "Kota Contoh", address: null, contactPerson: null, isVerified: true, mergedIntoId: null }]} majors={[{ id: "major-1", name: "Rekayasa Perangkat Lunak" }]} />);
  expect(html).toContain("Tambah siswa");
  expect(html).toContain("Undang staf");
  expect(html).toContain("Buat placement");
  expect(html).toContain("Pilih siswa");
  expect(html).not.toContain("TenantId");
  expect(html).not.toContain("StudentId");
  expect(html).not.toContain("CompanyId");
  expect(html).not.toContain("TeacherId");
  expect(html).not.toContain("temporary password");
});
