"use client";

import { useState } from "react";
import type { CompanyDto, RubricDto } from "@/lib/apiTypes";
import { RubricTemplateEditor } from "./RubricTemplateEditor";

export function RubricTemplateWorkspace({ initialRubric, rubrics, companies, periodLabel }: { initialRubric: RubricDto | null; rubrics: RubricDto[]; companies: CompanyDto[]; periodLabel: string }) {
  const [selectedId, setSelectedId] = useState(initialRubric?.id ?? "new-default");
  const selectedRubric = selectedId === "new-default" ? null : rubrics.find((rubric) => rubric.id === selectedId) ?? null;
  const isNewCompanyTemplate = selectedId.startsWith("new-company:");
  const newCompanyId = isNewCompanyTemplate ? selectedId.slice("new-company:".length) : null;

  return (
    <div className="flex flex-col gap-5">
      <label className="flex flex-col gap-1 text-sm font-medium text-ink">
        Template yang dikelola
        <select value={selectedId} onChange={(event) => setSelectedId(event.target.value)} className="h-11 border border-border bg-surface px-3 font-normal">
          {rubrics.map((rubric) => <option key={rubric.id} value={rubric.id}>{rubric.companyId ? `DUDI: ${companies.find((company) => company.id === rubric.companyId)?.name ?? "DUDI"}` : "Default sekolah"} · v{rubric.version}</option>)}
          <option value="new-default">Buat template default sekolah</option>
          {companies.map((company) => <option key={`new-${company.id}`} value={`new-company:${company.id}`}>Buat template DUDI: {company.name}</option>)}
        </select>
      </label>
      {selectedRubric?.companyId && <p className="border border-status-blue/30 bg-status-blue-bg p-3 text-sm text-ink">Kriteria ini khusus untuk DUDI yang dipilih dan akan menjadi dasar penilaian mentor industri.</p>}
      <RubricTemplateEditor key={selectedId} initialRubric={selectedRubric} initialCompanyId={selectedRubric?.companyId ?? newCompanyId} periodLabel={periodLabel} />
    </div>
  );
}
