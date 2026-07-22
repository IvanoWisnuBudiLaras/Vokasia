"use client";

import { useState } from "react";
import { VisitForm } from "./VisitForm";
import { VisitHistoryList } from "./VisitHistoryList";

/**
 * VOK-H5-E2 §1 — wrapper client tipis yang menghubungkan VisitForm+VisitHistoryList (dua
 * client component bersaudara yang perlu tahu satu sama lain: submit form -> riwayat refresh).
 * `page.tsx` di atasnya tetap Server Component (fetch placement+nama siswa), cuma render ini.
 */
export function VisitSection({ placementId }: { placementId: string }) {
  const [refreshKey, setRefreshKey] = useState(0);

  return (
    <div className="flex flex-col gap-6 lg:grid lg:grid-cols-2 lg:gap-6">
      <div>
        <h2 className="mb-2 text-sm font-semibold text-ink">Catat Kunjungan Baru</h2>
        <VisitForm placementId={placementId} onSubmitted={() => setRefreshKey((n) => n + 1)} />
      </div>
      <div>
        <h2 className="mb-2 text-sm font-semibold text-ink">Riwayat Kunjungan</h2>
        <VisitHistoryList placementId={placementId} refreshKey={refreshKey} />
      </div>
    </div>
  );
}
