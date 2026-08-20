"use client";

import { ButtonHTMLAttributes, forwardRef } from "react";

/** Material 3 tonal baseline for app-specific actions; domain behavior stays in React. */
export const MaterialButton = forwardRef<HTMLButtonElement, ButtonHTMLAttributes<HTMLButtonElement>>(function MaterialButton({ className = "", ...props }, ref) {
  return <button ref={ref} {...props} className={`min-h-11 rounded-[var(--radius-md)] border px-4 font-medium focus-visible:outline-2 focus-visible:outline-focus ${className}`} />;
});
