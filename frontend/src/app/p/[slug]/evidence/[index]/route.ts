import { NextResponse } from "next/server";

export async function GET(_request: Request, { params }: { params: Promise<{ slug: string; index: string }> }) {
  const { slug, index } = await params;
  const base = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const response = await fetch(`${base}/api/public/portfolio/${encodeURIComponent(slug)}/evidence/${encodeURIComponent(index)}`, { cache: "no-store" });
  if (!response.ok) return new NextResponse(null, { status: response.status });
  return new NextResponse(await response.arrayBuffer(), {
    status: 200,
    headers: {
      "Content-Type": response.headers.get("content-type") ?? "image/jpeg",
      "Cache-Control": "public, max-age=300",
    },
  });
}
