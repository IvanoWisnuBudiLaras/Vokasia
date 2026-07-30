import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import type { JournalDto } from "@/lib/apiTypes";
import { ApprovalCard } from "./ApprovalCard";

const journal: JournalDto = {
  id: "journal-1",
  slotId: "slot-1",
  placementId: "placement-1",
  text: "Membantu menyusun halaman profil perusahaan.",
  status: 1,
  mentorNote: null,
  submittedAt: "2026-07-27T09:00:00Z",
  approvedAt: null,
  photos: [],
  competencyIds: [],
};

describe("ApprovalCard", () => {
  test("aksi memakai Bahasa Indonesia sederhana dan ikon SVG", () => {
    const html = renderToStaticMarkup(
      <ApprovalCard
        journal={journal}
        studentName="Siswa Contoh"
        expanded
        selected={false}
        busy={false}
        onToggleExpand={() => undefined}
        onToggleSelect={() => undefined}
        onApprove={() => undefined}
        onReject={() => undefined}
      />
    );

    expect(html).toContain("Setujui Jurnal");
    expect(html).toContain("Tolak Jurnal");
    expect(html).toContain("<svg");
    expect(html).not.toContain("Approve");
  });
});
