import { NextResponse } from "next/server";

export async function GET(request: Request, { params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const base = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const response = await fetch(`${base}/api/verify/${encodeURIComponent(code)}/pdf`, { cache: "no-store" });
  if (!response.ok) return new NextResponse(null, { status: response.status });
  const disposition = new URL(request.url).searchParams.get("download") === "1" ? "attachment" : "inline";
  return new NextResponse(await response.arrayBuffer(), {
    status: 200,
    headers: {
      "Content-Type": "application/pdf",
      "Content-Disposition": `${disposition}; filename="sertifikat-${encodeURIComponent(code)}.pdf"`,
      "Cache-Control": "private, no-store",
    },
  });
}
