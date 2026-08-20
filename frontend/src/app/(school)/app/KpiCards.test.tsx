import { expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { KpiCards } from "./KpiCards";

test("TenantAdmin summary prioritizes operational follow-up links", () => {
  const html = renderToStaticMarkup(
    <KpiCards journalTodayPct={82.5} pendingApprovals={3} lateVisits={1} flaggedCount={2} />,
  );

  expect(html).toContain("Siswa perlu tindak lanjut");
  expect(html).toContain("Approval jurnal tertunda");
  expect(html).toContain("Kunjungan terlambat");
  expect(html).toContain('href="#siswa-bermasalah"');
  expect(html).not.toContain("rounded-2xl");
});
