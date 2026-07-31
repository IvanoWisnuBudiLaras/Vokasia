"use client";

import { Card } from "@/components/ui";

export function DashboardCharts() {
  const revenueData = [
    { month: "Jan", amount: 1200000 },
    { month: "Feb", amount: 1800000 },
    { month: "Mar", amount: 2400000 },
    { month: "Apr", amount: 2900000 },
    { month: "Mei", amount: 3200000 },
    { month: "Jun", amount: 3497000 },
  ];

  const maxRevenue = 4000000;

  const weeklyAttendance = [
    { day: "Senin", pct: 94 },
    { day: "Selasa", pct: 96 },
    { day: "Rabu", pct: 92 },
    { day: "Kamis", pct: 95 },
    { day: "Jumat", pct: 91 },
  ];

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      {/* 1. Grafik Tren Pendapatan MRR (Bar & Line Chart SVG) */}
      <Card title="Tren Pendapatan & MRR (6 Bulan)">
        <div className="mt-2 flex flex-col gap-4">
          <div className="flex items-baseline justify-between">
            <span className="text-xs text-ink-muted">Target Q2: Rp 4.000.000</span>
            <span className="text-sm font-semibold text-status-green">+18.5% MoM</span>
          </div>

          <div className="flex h-44 items-end gap-3 border-b border-border pb-2 pt-4">
            {revenueData.map((d) => {
              const heightPct = (d.amount / maxRevenue) * 100;
              return (
                <div key={d.month} className="group relative flex flex-1 flex-col items-center gap-1">
                  {/* Tooltip on hover */}
                  <div className="pointer-events-none absolute -top-8 z-10 hidden whitespace-nowrap rounded-[var(--radius-sm)] bg-ink px-2 py-1 text-xs text-surface shadow group-hover:block">
                    Rp {d.amount.toLocaleString("id-ID")}
                  </div>

                  <div className="w-full rounded-t-[var(--radius-sm)] bg-primary-muted transition-all group-hover:bg-primary" style={{ height: `${heightPct}%` }}>
                    <div className="h-1.5 w-full rounded-t-[var(--radius-sm)] bg-primary" />
                  </div>
                  <span className="mt-1 text-xs text-ink-muted">{d.month}</span>
                </div>
              );
            })}
          </div>
        </div>
      </Card>

      {/* 2. Grafik Distribusi Status RAG Siswa PKL */}
      <Card title="Statistik Kehadiran & Status Siswa (Minggu Ini)">
        <div className="mt-2 flex flex-col gap-4">
          {/* Progress Bars */}
          <div className="flex flex-col gap-2.5">
            <div>
              <div className="flex items-center justify-between text-xs font-medium">
                <span className="text-status-green">🟢 Hijau (Beres / Absen Lengkap)</span>
                <span className="text-ink">74 Siswa (82%)</span>
              </div>
              <div className="mt-1 h-2.5 w-full overflow-hidden rounded-full bg-status-green-bg">
                <div className="h-full rounded-full bg-status-green" style={{ width: "82%" }} />
              </div>
            </div>

            <div>
              <div className="flex items-center justify-between text-xs font-medium">
                <span className="text-status-amber">🟡 Kuning (Perlu Perhatian)</span>
                <span className="text-ink">11 Siswa (12%)</span>
              </div>
              <div className="mt-1 h-2.5 w-full overflow-hidden rounded-full bg-status-amber-bg">
                <div className="h-full rounded-full bg-status-amber" style={{ width: "12%" }} />
              </div>
            </div>

            <div>
              <div className="flex items-center justify-between text-xs font-medium">
                <span className="text-status-red">🔴 Merah (Bermasalah / Ghosting Alert)</span>
                <span className="text-ink">5 Siswa (6%)</span>
              </div>
              <div className="mt-1 h-2.5 w-full overflow-hidden rounded-full bg-status-red-bg">
                <div className="h-full rounded-full bg-status-red" style={{ width: "6%" }} />
              </div>
            </div>
          </div>

          {/* Mingguan Bar Mini */}
          <div className="mt-2 border-t border-border pt-3">
            <span className="text-xs text-ink-muted">Tingkat Pengisian Jurnal Harian (%)</span>
            <div className="mt-2 flex items-center justify-between gap-2">
              {weeklyAttendance.map((w) => (
                <div key={w.day} className="flex flex-col items-center gap-1">
                  <span className="text-[10px] font-medium text-ink">{w.pct}%</span>
                  <div className="h-10 w-3 rounded-full bg-surface-muted">
                    <div className="w-full rounded-full bg-primary" style={{ height: `${w.pct}%` }} />
                  </div>
                  <span className="text-[10px] text-ink-muted">{w.day.slice(0, 3)}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </Card>
    </div>
  );
}
