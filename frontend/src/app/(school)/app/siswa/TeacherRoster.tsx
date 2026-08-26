"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { EmptyState, Icon, StatusBadge } from "@/components/ui";
import type { CompanyDto, PlacementDto, StudentDto } from "@/lib/apiTypes";

interface Props {
  students: StudentDto[];
  placements: PlacementDto[];
  companies: CompanyDto[];
  periodId: string;
}

export function TeacherRoster({ students, placements, companies }: Props) {
  const [query, setQuery] = useState("");
  const [classroom, setClassroom] = useState("all");
  const companyById = useMemo(() => new Map(companies.map((company) => [company.id, company.name])), [companies]);
  const studentById = useMemo(() => new Map(students.map((student) => [student.id, student])), [students]);
  const classrooms = useMemo(() => [...new Set(students.map((student) => student.classroom).filter(Boolean))].sort(), [students]);
  const rows = useMemo(() => placements.map((placement) => ({ placement, student: studentById.get(placement.studentId), company: companyById.get(placement.companyId) ?? "DUDI belum tercatat" }))
    .filter(({ student, company }) => student && (classroom === "all" || student.classroom === classroom) && `${student.fullName} ${student.classroom} ${company}`.toLowerCase().includes(query.toLowerCase().trim()))
    .sort((a, b) => a.student!.fullName.localeCompare(b.student!.fullName, "id")), [placements, studentById, companyById, classroom, query]);

  return (
    <section aria-labelledby="roster-heading" className="flex flex-col gap-4">
      <div className="flex flex-col gap-3 border-y border-border py-4 sm:flex-row sm:items-end">
        <label className="flex-1 text-sm font-medium text-ink">Cari siswa
          <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Nama atau kelas" className="mt-1 h-11 w-full border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:border-primary focus-visible:outline-2 focus-visible:outline-focus" />
        </label>
        <label className="text-sm font-medium text-ink">Kelas
          <select value={classroom} onChange={(event) => setClassroom(event.target.value)} className="mt-1 h-11 w-full border border-border bg-surface px-3 text-sm text-ink sm:w-48">
            <option value="all">Semua kelas</option>{classrooms.map((item) => <option key={item} value={item}>{item}</option>)}
          </select>
        </label>
      </div>
      <div className="flex items-center justify-between gap-3">
        <h2 id="roster-heading" className="text-sm font-semibold text-ink">{rows.length} siswa</h2>
        <Link href="/app/bimbingan" className="text-sm font-semibold text-primary underline underline-offset-4">Buka bimbingan</Link>
      </div>
      {rows.length === 0 ? <EmptyState icon={<Icon name="graduation-cap" size={32} />} title="Tidak ada siswa" description="Tidak ada siswa yang cocok dengan pencarian ini." /> : (
        <>
        <div className="border-y border-border lg:hidden">
          <ul className="divide-y divide-border">
            {rows.map(({ placement, student, company }) => <li key={placement.id} className="flex flex-col gap-2 py-4">
              <span className="font-medium text-ink">{student!.fullName}</span>
              <span className="text-sm text-ink-muted">{company}</span>
              <span className="text-sm text-ink-muted">{student!.classroom} · <StatusBadge status={placement.status === 0 ? "green" : "amber"} label={placement.status === 0 ? "Aktif" : placement.status === 1 ? "Selesai" : "Dihentikan"} /></span>
              <Link href={`/app/bimbingan/${placement.id}`} className="min-h-11 self-start pt-2 font-semibold text-primary underline underline-offset-4">Lihat bimbingan</Link>
            </li>)}
          </ul>
        </div>
        <div className="hidden overflow-x-auto border-y border-border lg:block">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead className="border-b border-border bg-surface-muted text-xs uppercase tracking-wide text-ink-muted"><tr><th className="px-3 py-3 font-semibold">Siswa</th><th className="px-3 py-3 font-semibold">Kelas</th><th className="px-3 py-3 font-semibold">DUDI</th><th className="px-3 py-3 font-semibold">Status placement</th><th className="px-3 py-3" /></tr></thead>
            <tbody className="divide-y divide-border">
              {rows.map(({ placement, student, company }) => <tr key={placement.id}>
                <td className="px-3 py-4 font-medium text-ink">{student!.fullName}</td>
                <td className="px-3 py-4 text-ink-muted">{student!.classroom}</td>
                <td className="px-3 py-4 text-ink-muted">{company}</td>
                <td className="px-3 py-4"><StatusBadge status={placement.status === 0 ? "green" : "amber"} label={placement.status === 0 ? "Aktif" : placement.status === 1 ? "Selesai" : "Dihentikan"} /></td>
                <td className="px-3 py-4 text-right"><Link href={`/app/bimbingan/${placement.id}`} className="font-semibold text-primary underline underline-offset-4">Lihat bimbingan</Link></td>
              </tr>)}
            </tbody>
          </table>
        </div>
        </>
      )}
    </section>
  );
}
