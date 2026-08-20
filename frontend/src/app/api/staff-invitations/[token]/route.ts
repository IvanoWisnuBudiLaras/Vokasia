import { NextResponse } from "next/server";

export async function GET(request: Request, { params }: { params: Promise<{ token: string }> }) {
  const { token } = await params;
  const base = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const response = await fetch(`${base}/api/staff-invitations/${encodeURIComponent(token)}`, { cache: "no-store" });
  return new NextResponse(await response.text(), { status: response.status, headers: { "content-type": "application/json" } });
}
