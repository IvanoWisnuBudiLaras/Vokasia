"use client";

import { useState } from "react";
import { apiClient, ApiError } from "@/lib/apiClient";
import { UserRole, type CompanyDto, type MajorOptionDto, type PeriodSummary, type PlacementDto, type SchoolUserDto, type StudentDto } from "@/lib/apiTypes";

interface Props { periods: PeriodSummary[]; students: StudentDto[]; staff: SchoolUserDto[]; companies: CompanyDto[]; majors: MajorOptionDto[]; }

export function OperationsWorkspace({ periods, students: initialStudents, staff: initialStaff, companies: initialCompanies, majors }: Props) {
  const [students, setStudents] = useState(initialStudents);
  const [staff, setStaff] = useState(initialStaff);
  const [companies, setCompanies] = useState(initialCompanies);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function run(action: () => Promise<void>) {
    setBusy(true); setError(null); setMessage(null);
    try { await action(); } catch (err) { setError(err instanceof ApiError ? err.message : "Perubahan belum tersimpan. Coba lagi."); } finally { setBusy(false); }
  }

  return (
    <div className="flex flex-col gap-6">
      <div><h1 className="text-xl font-semibold text-ink">Operasi PKL</h1><p className="text-sm text-ink-muted">Kelola orang, DUDI, dan placement dari satu ruang kerja.</p></div>
      {message && <p role="status" className="border border-status-green/40 bg-status-green-bg p-3 text-sm text-ink">{message}</p>}
      {error && <p role="alert" className="border border-status-red/40 bg-status-red-bg p-3 text-sm text-status-red">{error}</p>}

      <section aria-labelledby="students-heading" className="border-y border-border py-4">
        <h2 id="students-heading" className="mb-3 text-sm font-semibold text-ink">Siswa</h2>
        <StudentForm majors={majors} busy={busy} onCreated={(student) => { setStudents((items) => [student, ...items]); setMessage("Siswa berhasil ditambahkan."); }} onRun={run} />
        <ul className="mt-4 divide-y divide-border border-y border-border">{students.map((student) => <li key={student.id} className="py-2 text-sm text-ink">{student.fullName}<span className="ml-2 text-xs text-ink-muted">{student.classroom}</span></li>)}</ul>
      </section>

      <section aria-labelledby="staff-heading" className="border-y border-border py-4">
        <h2 id="staff-heading" className="mb-3 text-sm font-semibold text-ink">Staf</h2>
        <StaffForm busy={busy} onInvited={(user) => { setStaff((items) => [user, ...items]); setMessage("Undangan dikirim."); }} onRun={run} />
        <ul className="mt-4 divide-y divide-border border-y border-border">{staff.filter((user) => user.role !== UserRole.Student).map((user) => <li key={user.id} className="py-2 text-sm text-ink">{user.fullName}<span className="ml-2 text-xs text-ink-muted">{user.role}</span></li>)}</ul>
      </section>

      <PlacementSection periods={periods} students={students} staff={staff} companies={companies} busy={busy} onCreated={(placement) => { setMessage(`Placement berhasil dibuat: ${placement.id}`); }} onCompanyCreated={(company) => { setCompanies((items) => [company, ...items]); setMessage("DUDI berhasil ditambahkan."); }} onRun={run} />
    </div>
  );
}

function StudentForm({ majors, busy, onCreated, onRun }: { majors: MajorOptionDto[]; busy: boolean; onCreated: (student: StudentDto) => void; onRun: (action: () => Promise<void>) => Promise<void> }) {
  const [fullName, setFullName] = useState(""); const [nisn, setNisn] = useState(""); const [classroom, setClassroom] = useState(""); const [majorId, setMajorId] = useState(majors[0]?.id ?? "");
  return <form className="grid gap-3 md:grid-cols-4" onSubmit={(event) => { event.preventDefault(); void onRun(async () => { const student = await apiClient.post<StudentDto>("/students", { fullName, nisn: nisn || null, classroom, majorId }); onCreated(student); setFullName(""); setNisn(""); setClassroom(""); }); }}>
    <label className="text-sm text-ink">Nama<input aria-label="Nama siswa" required value={fullName} onChange={(e) => setFullName(e.target.value)} className="mt-1 h-11 w-full border border-border px-3" /></label>
    <label className="text-sm text-ink">NISN<input aria-label="NISN siswa" value={nisn} onChange={(e) => setNisn(e.target.value)} className="mt-1 h-11 w-full border border-border px-3" /></label>
    <label className="text-sm text-ink">Kelas<input aria-label="Kelas siswa" required value={classroom} onChange={(e) => setClassroom(e.target.value)} className="mt-1 h-11 w-full border border-border px-3" /></label>
    <label className="text-sm text-ink">Jurusan<select aria-label="Jurusan siswa" required value={majorId} onChange={(e) => setMajorId(e.target.value)} className="mt-1 h-11 w-full border border-border px-3">{majors.map((major) => <option key={major.id} value={major.id}>{major.name}</option>)}</select></label>
    <button type="submit" disabled={busy} className="min-h-11 border border-primary px-4 text-sm font-medium text-primary md:col-span-4 md:justify-self-start">Tambah siswa</button>
  </form>;
}

function StaffForm({ busy, onInvited, onRun }: { busy: boolean; onInvited: (user: SchoolUserDto) => void; onRun: (action: () => Promise<void>) => Promise<void> }) {
  const [fullName, setFullName] = useState(""); const [email, setEmail] = useState(""); const [role, setRole] = useState(String(UserRole.Teacher));
  return <form className="grid gap-3 md:grid-cols-4" onSubmit={(event) => { event.preventDefault(); void onRun(async () => { const result = await apiClient.post<{ user: SchoolUserDto }>("/school-users", { fullName, email, role: Number(role) }); onInvited(result.user); setFullName(""); setEmail(""); }); }}>
    <label className="text-sm text-ink">Nama<input aria-label="Nama staf" required value={fullName} onChange={(e) => setFullName(e.target.value)} className="mt-1 h-11 w-full border border-border px-3" /></label>
    <label className="text-sm text-ink">Email<input aria-label="Email staf" required type="email" value={email} onChange={(e) => setEmail(e.target.value)} className="mt-1 h-11 w-full border border-border px-3" /></label>
    <label className="text-sm text-ink">Peran<select aria-label="Peran staf" value={role} onChange={(e) => setRole(e.target.value)} className="mt-1 h-11 w-full border border-border px-3"><option value={UserRole.Teacher}>Teacher</option><option value={UserRole.DeptHead}>DeptHead</option><option value={UserRole.TenantAdmin}>TenantAdmin</option></select></label>
    <button type="submit" disabled={busy} className="min-h-11 border border-primary px-4 text-sm font-medium text-primary">Undang staf</button>
  </form>;
}

function PlacementSection({ periods, students, staff, companies, busy, onCreated, onCompanyCreated, onRun }: { periods: PeriodSummary[]; students: StudentDto[]; staff: SchoolUserDto[]; companies: CompanyDto[]; busy: boolean; onCreated: (placement: PlacementDto) => void; onCompanyCreated: (company: CompanyDto) => void; onRun: (action: () => Promise<void>) => Promise<void> }) {
  const [studentId, setStudentId] = useState(""); const [companyId, setCompanyId] = useState(""); const [periodId, setPeriodId] = useState(periods[0]?.id ?? ""); const [teacherId, setTeacherId] = useState(staff.find((user) => user.role === UserRole.Teacher)?.id ?? ""); const [mentorEmail, setMentorEmail] = useState(""); const [companyName, setCompanyName] = useState("");
  return <section aria-labelledby="placement-heading" className="border-y border-border py-4"><h2 id="placement-heading" className="mb-3 text-sm font-semibold text-ink">Placement</h2>
    <form className="grid gap-3 md:grid-cols-2" onSubmit={(event) => { event.preventDefault(); void onRun(async () => { const placement = await apiClient.post<PlacementDto>("/placements", { studentId, companyId, periodId, teacherId, mentorEmail: mentorEmail || null }); onCreated(placement); }); }}>
      <label className="text-sm text-ink">Siswa<select aria-label="Siswa placement" required value={studentId} onChange={(e) => setStudentId(e.target.value)} className="mt-1 h-11 w-full border border-border px-3"><option value="">Pilih siswa</option>{students.map((student) => <option key={student.id} value={student.id}>{student.fullName}</option>)}</select></label>
      <label className="text-sm text-ink">DUDI<select aria-label="DUDI placement" required value={companyId} onChange={(e) => setCompanyId(e.target.value)} className="mt-1 h-11 w-full border border-border px-3"><option value="">Pilih DUDI</option>{companies.map((company) => <option key={company.id} value={company.id}>{company.name}</option>)}</select></label>
      <label className="text-sm text-ink">Periode<select aria-label="Periode placement" required value={periodId} onChange={(e) => setPeriodId(e.target.value)} className="mt-1 h-11 w-full border border-border px-3">{periods.map((period) => <option key={period.id} value={period.id}>{period.name}</option>)}</select></label>
      <label className="text-sm text-ink">Guru<select aria-label="Guru placement" required value={teacherId} onChange={(e) => setTeacherId(e.target.value)} className="mt-1 h-11 w-full border border-border px-3"><option value="">Pilih guru</option>{staff.filter((user) => user.role === UserRole.Teacher).map((user) => <option key={user.id} value={user.id}>{user.fullName}</option>)}</select></label>
      <label className="text-sm text-ink md:col-span-2">Email mentor industri<input aria-label="Email mentor placement" type="email" value={mentorEmail} onChange={(e) => setMentorEmail(e.target.value)} className="mt-1 h-11 w-full border border-border px-3" /></label>
      <button type="submit" disabled={busy} className="min-h-11 border border-primary bg-primary px-4 text-sm font-medium text-primary-ink md:col-span-2 md:justify-self-start">Buat placement</button>
    </form>
    <form className="mt-5 flex gap-3" onSubmit={(event) => { event.preventDefault(); void onRun(async () => { const company = await apiClient.post<CompanyDto>("/companies/propose", { name: companyName, sector: null, city: null, address: null, contactPerson: null }); onCompanyCreated(company); setCompanyName(""); }); }}><label className="flex-1 text-sm text-ink">Tambah DUDI<input aria-label="Nama DUDI baru" required value={companyName} onChange={(e) => setCompanyName(e.target.value)} className="mt-1 h-11 w-full border border-border px-3" /></label><button type="submit" disabled={busy} className="mt-6 min-h-11 border border-border px-4 text-sm font-medium text-ink">Tambah DUDI</button></form>
  </section>;
}
