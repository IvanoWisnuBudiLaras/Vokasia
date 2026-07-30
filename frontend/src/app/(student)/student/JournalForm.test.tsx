import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import type { CompetencyDto, JournalSlotDto } from "@/lib/apiTypes";
import { JournalForm } from "./JournalForm";

const slot: JournalSlotDto = {
  id: "slot-1",
  date: "2026-07-27",
  status: 0,
};

const competencies: CompetencyDto[] = [
  { id: "competency-1", name: "Menyusun antarmuka web", majorId: "major-1" },
];

describe("JournalForm", () => {
  test("menjelaskan pemulihan draf sesi tanpa menjanjikan penyimpanan foto", () => {
    const html = renderToStaticMarkup(
      <JournalForm
        slot={slot}
        competencies={competencies}
        draftScope="tenant-1:student-1"
        onSubmitted={() => undefined}
      />
    );

    expect(html).toContain("Draf teks dan kompetensi disimpan sementara di tab ini.");
    expect(html).toContain("Foto perlu dipilih kembali");
    expect(html).toContain("Kirim Jurnal");
  });

  test("alasan penolakan diumumkan sebagai status penting", () => {
    const html = renderToStaticMarkup(
      <JournalForm
        slot={slot}
        competencies={competencies}
        draftScope="tenant-1:student-1"
        rejectedReason="Tambahkan rincian alat yang digunakan."
        onSubmitted={() => undefined}
      />
    );

    expect(html).toContain("role=\"alert\"");
    expect(html).toContain("Tambahkan rincian alat yang digunakan.");
  });
});
