"use client";

import { RouteErrorState } from "@/components/RouteErrorState";

export default function SchoolRouteError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return <RouteErrorState reset={reset} />;
}
