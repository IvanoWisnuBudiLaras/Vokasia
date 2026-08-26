import { NextResponse } from "next/server";

export async function GET(_request: Request, { params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const base = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const response = await fetch(`${base}/api/public/portfolio/${encodeURIComponent(slug)}/cv`, { cache: "no-store" });
  if (!response.ok) return new NextResponse(null, { status: response.status });

  return new NextResponse(await response.arrayBuffer(), {
    status: 200,
    headers: {
      "Content-Type": "application/pdf",
      "Content-Disposition": `attachment; filename="cv-${encodeURIComponent(slug)}.pdf"`,
      "Cache-Control": "private, no-store",
    },
  });
}
