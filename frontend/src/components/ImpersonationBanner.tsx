import { getSession } from "@/lib/session";
import { Icon } from "@/components/ui";
import { EndImpersonationButton } from "./EndImpersonationButton";

/**
 * VOK-H6-E3 §2 — banner "sedang impersonasi" (AC ticket literal). Server Component: baca cookie
 * "lite" (lib/session.ts) SEKALI per navigasi, TANPA panggil Redis/DB (sama alasan getSessionEdge
 * ada) — session.impersonatorName hanya terisi bila StartImpersonation (BFF) baru saja menimpa
 * cookie ini. Dipasang pada setiap layout role terlindungi agar tetap seragam untuk semua target
 * impersonasi tanpa membuat layout publik bergantung pada cookies() dan menjadi request-dynamic.
 */
export async function ImpersonationBanner() {
  const session = await getSession();
  if (!session?.impersonatorName) {
    return null;
  }

  return (
    <div className="sticky top-0 z-50 flex flex-wrap items-center justify-between gap-2 bg-status-amber-bg px-4 py-2 text-sm font-medium text-status-amber">
      <span className="inline-flex items-start gap-2">
        <Icon name="warning" size={20} className="mt-0.5 shrink-0" />
        <span>
          {session.impersonatorName} sedang login sebagai <strong>{session.name}</strong> — semua aksi tercatat di audit log.
        </span>
      </span>
      <EndImpersonationButton />
    </div>
  );
}
