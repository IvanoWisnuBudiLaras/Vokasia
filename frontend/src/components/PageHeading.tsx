import type { ReactNode } from "react";

interface PageHeadingProps {
  title: string;
  description?: string;
  eyebrow?: string;
  action?: ReactNode;
}

/** Judul halaman seragam untuk workspace belajar dan operasi. */
export function PageHeading({ title, description, eyebrow, action }: PageHeadingProps) {
  return (
    <div className="flex flex-col gap-3 border-b border-border pb-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        {eyebrow && <p className="mb-1 text-xs font-semibold tracking-wide text-primary">{eyebrow}</p>}
        <h1 className="text-xl font-bold tracking-tight text-ink sm:text-2xl">{title}</h1>
        {description && <p className="mt-1 max-w-2xl text-sm leading-6 text-ink-muted">{description}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}
