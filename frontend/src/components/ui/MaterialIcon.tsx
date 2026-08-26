"use client";

import { Icon } from "@iconify/react";

export const materialSymbols = {
  journal: "material-symbols-rounded:assignment",
  student: "material-symbols-rounded:person",
  students: "material-symbols-rounded:group",
  school: "material-symbols-rounded:school",
  company: "material-symbols-rounded:business",
  warning: "material-symbols-rounded:warning",
  verified: "material-symbols-rounded:verified",
  billing: "material-symbols-rounded:payments",
  period: "material-symbols-rounded:calendar-month",
  audit: "material-symbols-rounded:history",
  settings: "material-symbols-rounded:settings",
  logout: "material-symbols-rounded:logout",
  visit: "material-symbols-rounded:location-on",
} as const;

export function MaterialIcon({ name, label, decorative = false }: { name: keyof typeof materialSymbols; label?: string; decorative?: boolean }) {
  return (
    <span
      data-icon={materialSymbols[name]}
      role={label && !decorative ? "img" : undefined}
      aria-hidden={decorative || !label ? true : undefined}
      aria-label={decorative ? undefined : label}
      className="inline-flex items-center justify-center"
    >
      <Icon icon={materialSymbols[name]} width="1.25em" height="1.25em" />
    </span>
  );
}
