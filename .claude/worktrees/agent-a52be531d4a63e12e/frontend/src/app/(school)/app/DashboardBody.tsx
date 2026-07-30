"use client";

import { useState } from "react";
import type { DashboardFlaggedStudentDto } from "@/lib/apiTypes";
import { ProblemStudentList } from "./ProblemStudentList";
import { StudentDetailDrawer } from "./StudentDetailDrawer";

export interface DashboardBodyProps {
  flagged: DashboardFlaggedStudentDto[];
  periodId: string;
}

/**
 * VOK-H4-E2 — pemilik state "siswa mana yang dipilih" utk StudentDetailDrawer. Diekstrak jadi
 * client component terisolasi (pola sama SidebarNav.tsx/OfflineBanner.tsx, D19) krn butuh
 * useState — SchoolDashboardPage (page.tsx) di sekitarnya tetap Server Component.
 */
export function DashboardBody({ flagged, periodId }: DashboardBodyProps) {
  const [selected, setSelected] = useState<DashboardFlaggedStudentDto | null>(null);

  return (
    <>
      <ProblemStudentList items={flagged} onSelect={setSelected} />
      <StudentDetailDrawer student={selected} periodId={periodId} onClose={() => setSelected(null)} />
    </>
  );
}
