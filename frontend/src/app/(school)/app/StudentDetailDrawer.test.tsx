import { expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { StudentDetailActions } from "./StudentDetailDrawer";

test("teacher student detail exposes journal, visit, and assessment actions", () => {
  const html = renderToStaticMarkup(<StudentDetailActions placementId="placement-1" />);
  expect(html).toContain("Lihat jurnal &amp; beri komentar");
  expect(html).toContain("Catat kunjungan");
  expect(html).toContain("Isi penilaian");
  expect(html).toContain("/app/penilaian?placementId=");
  expect(html).not.toContain("teacherId=");
});
