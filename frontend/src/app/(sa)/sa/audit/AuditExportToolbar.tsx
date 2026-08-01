"use client";

import { TableExportToolbar } from "@/components/ui";
import type { AuditDto } from "@/lib/apiTypes";

export interface AuditExportToolbarProps {
  logs: AuditDto[];
}

export function AuditExportToolbar({ logs }: AuditExportToolbarProps) {
  const formattedLogs = logs.map((log) => ({
    ...log,
    waktuFormatted: new Date(log.createdAt).toLocaleString("id-ID"),
    actorCombined: log.actingAsUserId
      ? `${log.actorUserId} (sbg ${log.actingAsUserId})`
      : log.actorUserId,
  }));

  return (
    <TableExportToolbar
      data={formattedLogs}
      filename="audit_log_kepatuhan_vokasia"
      title="Audit Log & Compliance Trail Platform Vokasia"
      columns={[
        { key: "waktuFormatted", label: "Waktu" },
        { key: "actorCombined", label: "Aktor (User ID)" },
        { key: "action", label: "Aksi" },
        { key: "entity", label: "Entitas" },
        { key: "entityId", label: "ID Entitas" },
        { key: "metaJson", label: "Metadata / Payload" },
      ]}
    />
  );
}
