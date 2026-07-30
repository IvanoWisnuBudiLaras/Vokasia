"use client";

import { RouteErrorState } from "@/components/RouteErrorState";

export default function MentorRouteError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return <RouteErrorState reset={reset} theme="sekolah" />;
}
