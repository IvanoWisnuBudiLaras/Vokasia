import type { ReactNode, SVGProps } from "react";

export type IconName =
  | "arrow-left"
  | "arrow-right"
  | "award"
  | "bell"
  | "briefcase-business"
  | "building-2"
  | "calendar-days"
  | "camera"
  | "check"
  | "chevron-down"
  | "chevron-up"
  | "clipboard-check"
  | "download"
  | "file-pen-line"
  | "file-text"
  | "flag"
  | "graduation-cap"
  | "home"
  | "image"
  | "layout-dashboard"
  | "list-checks"
  | "map-pin"
  | "message-square-text"
  | "notebook-pen"
  | "package"
  | "receipt"
  | "signature"
  | "warning"
  | "x";

export type IconSize = 16 | 20 | 24 | 32;

export interface IconProps
  extends Omit<SVGProps<SVGSVGElement>, "children" | "height" | "name" | "width"> {
  name: IconName;
  size?: IconSize;
}

const glyphs: Record<IconName, ReactNode> = {
  "arrow-left": (
    <>
      <path d="m15 18-6-6 6-6" />
      <path d="M21 12H9" />
    </>
  ),
  "arrow-right": (
    <>
      <path d="M3 12h12" />
      <path d="m9 6 6 6-6 6" />
    </>
  ),
  award: (
    <>
      <circle cx="12" cy="8" r="6" />
      <path d="M15.5 13 17 22l-5-3-5 3 1.5-9" />
    </>
  ),
  bell: (
    <>
      <path d="M10.3 21h3.4" />
      <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" />
    </>
  ),
  "briefcase-business": (
    <>
      <rect width="18" height="13" x="3" y="7" rx="2" />
      <path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
      <path d="M3 12a18 18 0 0 0 18 0" />
      <path d="M12 12v4" />
    </>
  ),
  "building-2": (
    <>
      <rect width="16" height="20" x="4" y="2" rx="2" />
      <path d="M9 22v-4h6v4" />
      <path d="M8 6h.01" />
      <path d="M16 6h.01" />
      <path d="M8 10h.01" />
      <path d="M16 10h.01" />
      <path d="M8 14h.01" />
      <path d="M16 14h.01" />
    </>
  ),
  "calendar-days": (
    <>
      <rect width="18" height="18" x="3" y="4" rx="2" />
      <path d="M16 2v4" />
      <path d="M8 2v4" />
      <path d="M3 10h18" />
      <path d="M8 14h.01" />
      <path d="M12 14h.01" />
      <path d="M16 14h.01" />
      <path d="M8 18h.01" />
      <path d="M12 18h.01" />
    </>
  ),
  camera: (
    <>
      <path d="M14.5 4 16 6h3a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h3l1.5-2z" />
      <circle cx="12" cy="13" r="3" />
    </>
  ),
  check: <path d="m5 12 4 4L19 6" />,
  "chevron-down": <path d="m6 9 6 6 6-6" />,
  "chevron-up": <path d="m18 15-6-6-6 6" />,
  "clipboard-check": (
    <>
      <rect width="14" height="18" x="5" y="4" rx="2" />
      <path d="M9 4V2h6v2" />
      <path d="m9 13 2 2 4-4" />
    </>
  ),
  download: (
    <>
      <path d="M12 3v12" />
      <path d="m7 10 5 5 5-5" />
      <path d="M5 21h14" />
    </>
  ),
  "file-pen-line": (
    <>
      <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h8" />
      <path d="M14 2v6h6" />
      <path d="M9 13h3" />
      <path d="M9 17h2" />
      <path d="m14.5 18.5 4-4a1.4 1.4 0 0 1 2 2l-4 4-3 1z" />
    </>
  ),
  "file-text": (
    <>
      <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5z" />
      <path d="M14 2v6h6" />
      <path d="M8 13h8" />
      <path d="M8 17h6" />
    </>
  ),
  flag: (
    <>
      <path d="M5 22V4" />
      <path d="M5 4h11l-2 4 2 4H5" />
    </>
  ),
  "graduation-cap": (
    <>
      <path d="m2 10 10-5 10 5-10 5z" />
      <path d="M6 12v5c3 2 9 2 12 0v-5" />
      <path d="M22 10v6" />
    </>
  ),
  home: (
    <>
      <path d="m3 11 9-8 9 8" />
      <path d="M5 10v11h14V10" />
      <path d="M9 21v-6h6v6" />
    </>
  ),
  image: (
    <>
      <rect width="18" height="18" x="3" y="3" rx="2" />
      <circle cx="9" cy="9" r="2" />
      <path d="m21 15-4-4L5 21" />
    </>
  ),
  "layout-dashboard": (
    <>
      <rect width="7" height="9" x="3" y="3" rx="1" />
      <rect width="7" height="5" x="14" y="3" rx="1" />
      <rect width="7" height="9" x="14" y="12" rx="1" />
      <rect width="7" height="5" x="3" y="16" rx="1" />
    </>
  ),
  "list-checks": (
    <>
      <path d="m3 7 2 2 4-4" />
      <path d="M11 7h10" />
      <path d="m3 17 2 2 4-4" />
      <path d="M11 17h10" />
    </>
  ),
  "map-pin": (
    <>
      <path d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z" />
      <circle cx="12" cy="10" r="2.5" />
    </>
  ),
  "message-square-text": (
    <>
      <path d="M21 15a4 4 0 0 1-4 4H8l-5 3v-5a4 4 0 0 1-1-2V7a4 4 0 0 1 4-4h11a4 4 0 0 1 4 4z" />
      <path d="M8 9h8" />
      <path d="M8 13h6" />
    </>
  ),
  "notebook-pen": (
    <>
      <path d="M6 3h10a2 2 0 0 1 2 2v6" />
      <path d="M6 3v18h7" />
      <path d="M2 6h4" />
      <path d="M2 10h4" />
      <path d="M2 14h4" />
      <path d="M2 18h4" />
      <path d="m15.5 17.5 4-4a1.4 1.4 0 0 1 2 2l-4 4-3 1z" />
    </>
  ),
  package: (
    <>
      <path d="m7.5 4.2 9 5.2" />
      <path d="M21 16V8a2 2 0 0 0-1-1.7l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.7l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z" />
      <path d="m3.3 7 8.7 5 8.7-5" />
      <path d="M12 22V12" />
    </>
  ),
  receipt: (
    <>
      <path d="M4 2v20l2-2 2 2 2-2 2 2 2-2 2 2 2-2 2 2 2-2 2 2V2l-2 2-2-2-2 2-2-2-2 2-2-2-2 2-2-2Z" />
      <path d="M8 9h8" />
      <path d="M8 13h6" />
    </>
  ),
  signature: (
    <>
      <path d="M3 17c3-5 5-8 7-8 3 0-1 8 2 8 2 0 3-4 5-4 1.5 0 1 4 4 4" />
      <path d="M3 21h18" />
    </>
  ),
  warning: (
    <>
      <path d="M10.3 3.7 2.4 18a2 2 0 0 0 1.8 3h15.6a2 2 0 0 0 1.8-3L13.7 3.7a2 2 0 0 0-3.4 0Z" />
      <path d="M12 9v4" />
      <path d="M12 17h.01" />
    </>
  ),
  x: (
    <>
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </>
  ),
};

/**
 * Ikon garis ringan untuk UI. Ukuran dibatasi agar ritme visual tetap konsisten
 * dan warna selalu mengikuti konteks teks melalui currentColor.
 */
export function Icon({
  name,
  size = 20,
  "aria-label": ariaLabel,
  "aria-labelledby": ariaLabelledBy,
  ...props
}: IconProps) {
  const labelled = Boolean(ariaLabel || ariaLabelledBy);

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      focusable="false"
      aria-hidden={labelled ? undefined : true}
      aria-label={ariaLabel}
      aria-labelledby={ariaLabelledBy}
      role={labelled ? "img" : undefined}
      {...props}
    >
      {glyphs[name]}
    </svg>
  );
}
