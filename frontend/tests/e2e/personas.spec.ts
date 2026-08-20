import { test, expect, type Browser, type Page } from "@playwright/test";
import path from "node:path";

type Credentials = { email?: string; password?: string };

const credentials = {
  superAdmin: { email: process.env.E2E_SUPERADMIN_EMAIL, password: process.env.E2E_SUPERADMIN_PASSWORD },
  tenantAdmin: { email: process.env.E2E_TENANT_ADMIN_EMAIL, password: process.env.E2E_TENANT_ADMIN_PASSWORD },
  teacher: { email: process.env.E2E_TEACHER_EMAIL, password: process.env.E2E_TEACHER_PASSWORD },
  student: { email: process.env.E2E_STUDENT_EMAIL, password: process.env.E2E_STUDENT_PASSWORD },
  approvalStudent: { email: process.env.E2E_APPROVAL_STUDENT_EMAIL, password: process.env.E2E_APPROVAL_STUDENT_PASSWORD },
};

const demo = {
  healthyStudent: process.env.E2E_DEMO_HEALTHY_STUDENT ?? "DEMO-HEALTHY",
  redStudent: process.env.E2E_DEMO_RED_STUDENT ?? "DEMO-RED",
  rejectedStudent: process.env.E2E_DEMO_REJECTED_STUDENT ?? "DEMO-REJECTED",
  teacherPlacementId: process.env.E2E_TEACHER_PLACEMENT_ID,
  teacherAspect: process.env.E2E_TEACHER_ASPECT,
  majorName: process.env.E2E_MAJOR_NAME,
  teacherName: process.env.E2E_TEACHER_NAME,
  periodName: process.env.E2E_PERIOD_NAME,
  mentorToken: process.env.E2E_MENTOR_MAGIC_TOKEN,
  certificateCode: process.env.E2E_CERTIFICATE_CODE,
};

const journalPhotoPng = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
  "base64"
);

let mentorToken = demo.mentorToken;

function requireCredentials(persona: string, value: Credentials) {
  if (!value.email || !value.password) {
    throw new Error(`${persona} credentials are required for the clean-state E2E run`);
  }
}

function requireSetting(name: string, value: string | undefined): asserts value is string {
  if (!value) {
    throw new Error(`${name} is required for the clean-state E2E run`);
  }
}

async function installBrowserPolicyGuard(page: Page) {
  const failures: string[] = [];
  page.on("pageerror", (error) => failures.push(`pageerror:${error.message}`));
  page.on("requestfailed", (request) => {
    const detail = request.failure()?.errorText ?? "unknown";
    if (/cors|csp|mixed|blocked|err_failed/i.test(detail)) {
      failures.push(`requestfailed:${request.url()}:${detail}`);
    }
  });
  page.on("response", (response) => {
    if (response.status() >= 500) {
      failures.push(`response:${response.url()}:${response.status()}`);
    }
  });
  page.on("console", (message) => {
    if (message.type() === "error" && /cors|csp|mixed content|blocked|uncaught/i.test(message.text())) {
      failures.push(`console:${message.text()}`);
    }
  });
  return () => expect(failures, "relevant browser policy/application failures").toEqual([]);
}

async function login(page: Page, value: Credentials, destination: string) {
  await page.goto(`/api/auth/login?next=${encodeURIComponent(destination)}`);
  await page.getByLabel("Email").fill(value.email!);
  await page.getByLabel("Kata sandi", { exact: true }).fill(value.password!);
  await page.getByRole("button", { name: /masuk/i }).click();
  await expect(page).toHaveURL(new RegExp(`${destination.replace("/", "\\/")}(?:[?#].*)?$`));
}

async function ensurePendingJournal(browser: Browser, value: Credentials, text: string) {
  requireCredentials("Student fixture", value);
  const context = await browser.newContext({ baseURL: process.env.PLAYWRIGHT_BASE_URL });
  const studentPage = await context.newPage();
  const checkBrowser = await installBrowserPolicyGuard(studentPage);

  try {
    await login(studentPage, value, "/student");
    const journalText = studentPage.getByLabel("Apa yang kamu kerjakan hari ini?");
    const pendingStatus = studentPage.getByText(/menunggu persetujuan/i);
    await expect(journalText.or(pendingStatus)).toBeVisible();
    if (await journalText.isVisible()) {
      await journalText.fill(text);
      await studentPage.getByRole("button", { name: /kirim untuk ditinjau/i }).click();
    }
    await expect(pendingStatus).toBeVisible();
    checkBrowser();
  } finally {
    await context.close();
  }
}

test.describe.configure({ mode: "serial" });

test("SuperAdmin provisions a tenant and sees invitation onboarding state", async ({ page }) => {
  requireCredentials("SuperAdmin", credentials.superAdmin);
  const checkBrowser = await installBrowserPolicyGuard(page);
  await login(page, credentials.superAdmin, "/sa/tenants");

  await page.getByRole("button", { name: /tenant baru/i }).click();
  await page.getByLabel("Nama Sekolah").fill(`E2E ${Date.now()}`);
  await page.getByLabel("Kota").fill("Kota Contoh");
  await page.getByRole("button", { name: /lanjut: pilih plan/i }).click();
  await page.getByRole("button", { name: /lanjut: admin pertama/i }).click();
  await page.getByLabel("Nama Admin").fill("Admin E2E Contoh");
  await page.getByLabel("Email Admin").fill(`admin-${Date.now()}@e2e.example`);
  await page.getByRole("button", { name: /buat tenant dan kirim undangan/i }).click();

  await expect(page.getByRole("status")).toContainText("Undangan admin sudah dikirim");
  await expect(page.getByText(/temporary password|kata sandi sementara/i)).toHaveCount(0);
  await page.getByRole("link", { name: /invoice/i }).click();
  await expect(page).toHaveURL(/\/sa\/invoices/);
  await expect(page.getByRole("heading", { name: /invoice/i })).toBeVisible();
  checkBrowser();
});

test("TenantAdmin uploads payment proof through the browser storage flow", async ({ page }) => {
  requireCredentials("TenantAdmin", credentials.tenantAdmin);
  const checkBrowser = await installBrowserPolicyGuard(page);
  await login(page, credentials.tenantAdmin, "/app/billing");

  await page.goto("/app/operasi");
  requireSetting("E2E_MAJOR_NAME", demo.majorName);
  requireSetting("E2E_TEACHER_NAME", demo.teacherName);
  requireSetting("E2E_PERIOD_NAME", demo.periodName);
  requireSetting("E2E_TEACHER_PLACEMENT_ID", demo.teacherPlacementId);
  const runId = process.env.E2E_RUN_ID ?? "local";
  const studentName = `E2E Student ${runId}`;
  const companyName = `E2E DUDI ${runId}`;
  await page.getByLabel("Nama staf").fill(`E2E Staff ${runId}`);
  await page.getByLabel("Email staf").fill(`staff-${runId}@e2e.example`);
  await page.getByLabel("Peran staf").selectOption("Teacher");
  await page.getByRole("button", { name: /undang staf/i }).click();
  await expect(page.getByRole("status")).toContainText("Undangan dikirim");
  await page.getByLabel("Nama siswa").fill(studentName);
  await page.getByLabel("NISN siswa").fill(`E2E${Date.now()}`);
  await page.getByLabel("Kelas siswa").fill("XII-RPL-E2E");
  await page.getByLabel("Jurusan siswa").selectOption({ label: demo.majorName! });
  await page.getByRole("button", { name: /tambah siswa/i }).click();
  await expect(page.getByRole("listitem").filter({ hasText: studentName })).toBeVisible();
  await page.getByLabel("Nama DUDI baru").fill(companyName);
  await page.getByRole("button", { name: /tambah dudi/i }).click();
  await expect(page.getByText("DUDI berhasil ditambahkan.")).toBeVisible();
  await page.getByLabel("Siswa placement").selectOption({ label: studentName });
  await page.getByLabel("DUDI placement").selectOption({ label: companyName });
  await page.getByLabel("Periode placement").selectOption({ label: demo.periodName! });
  await page.getByLabel("Guru placement").selectOption({ label: demo.teacherName! });
  await page.getByRole("button", { name: /buat placement/i }).click();
  await expect(page.getByRole("status")).toContainText(/placement berhasil dibuat/i);

  const inviteResponse = await page.request.post("/api/proxy/mentor-invites", {
    data: { placementId: demo.teacherPlacementId, mentorName: "Mentor E2E Vokasia" },
  });
  if (!inviteResponse.ok()) {
    throw new Error(`Mentor invite failed (${inviteResponse.status()}): ${await inviteResponse.text()}`);
  }
  const invite = (await inviteResponse.json()) as { magicLinkUrl?: string };
  mentorToken = invite.magicLinkUrl ? new URL(invite.magicLinkUrl).searchParams.get("token") ?? undefined : undefined;
  requireSetting("mentor token returned by the invite flow", mentorToken);

  await page.goto("/app/billing");

  const invoiceRow = page
    .getByRole("row")
    .filter({ hasText: /belum bayar/i })
    .first();
  const invoicePeriod = await invoiceRow.getByRole("cell").first().innerText();
  const upload = invoiceRow.getByRole("button", { name: /unggah bukti transfer/i });
  await expect(upload).toBeVisible();
  await upload.click();
  await page.getByLabel("Bukti transfer").setInputFiles(path.join(__dirname, "fixtures", "payment-proof.pdf"));
  await page.getByRole("button", { name: /simpan bukti/i }).click();
  const updatedInvoiceRow = page.getByRole("row").filter({ hasText: invoicePeriod });
  await expect(updatedInvoiceRow.getByText(/bukti terkirim/i)).toBeVisible();
  checkBrowser();
});

test("Teacher reviews the deterministic exception and submits an assessment", async ({ page }) => {
  requireCredentials("Teacher", credentials.teacher);
  requireSetting("E2E_TEACHER_PLACEMENT_ID", demo.teacherPlacementId);
  requireSetting("E2E_TEACHER_ASPECT", demo.teacherAspect);
  const checkBrowser = await installBrowserPolicyGuard(page);
  await login(page, credentials.teacher, "/app");

  await expect(page.getByRole("heading", { name: /siapa yang membutuhkan perhatian/i })).toBeVisible();
  await page.getByRole("button", { name: new RegExp(demo.redStudent, "i") }).click();
  await expect(page.getByRole("heading", { name: new RegExp(demo.redStudent, "i") })).toBeVisible();
  await page.getByRole("link", { name: /lihat jurnal.*beri komentar/i }).click();
  const latestJournal = page.getByRole("main").getByRole("listitem").first();
  const teacherComment = `Tindak lanjut dicatat dari triase guru ${process.env.E2E_RUN_ID ?? Date.now()}.`;
  await latestJournal.getByLabel("Tambah komentar").fill(teacherComment);
  await latestJournal.getByRole("button", { name: /kirim komentar/i }).click();
  await expect(latestJournal.locator("p").filter({ hasText: teacherComment })).toBeVisible();
  await page.goBack();
  await page.getByRole("button", { name: new RegExp(demo.redStudent, "i") }).click();
  await expect(page.getByRole("link", { name: /catat kunjungan/i })).toBeVisible();
  await page.getByRole("link", { name: /isi penilaian/i }).click();
  await expect(page).toHaveURL(/\/app\/penilaian/);
  const score = page.getByRole("spinbutton", { name: new RegExp(`Nilai angka ${demo.teacherAspect!}`, "i") });
  await score.fill("80");
  await expect(page.getByRole("status").filter({ hasText: /tersimpan/i })).toBeVisible();
  checkBrowser();
});

test("Mentor approves a pending journal and rejects another with a reason", async ({ page, browser }) => {
  requireCredentials("Approval student", credentials.approvalStudent);
  requireCredentials("Rejected student", credentials.student);
  requireSetting("E2E_DEMO_HEALTHY_STUDENT", demo.healthyStudent);
  requireSetting("E2E_DEMO_REJECTED_STUDENT", demo.rejectedStudent);
  requireSetting("mentor token created by TenantAdmin", mentorToken);
  await ensurePendingJournal(browser, credentials.approvalStudent, "Jurnal untuk persetujuan mentor dari alur E2E.");
  await ensurePendingJournal(browser, credentials.student, "Jurnal untuk pengujian penolakan mentor dari alur E2E.");
  const checkBrowser = await installBrowserPolicyGuard(page);
  await page.goto(`/mentor-invite?token=${encodeURIComponent(mentorToken)}`);
  await page.getByRole("link", { name: /masuk sebagai mentor/i }).click();
  await expect(page).toHaveURL(/\/mentor/);

  const approvalCard = page.getByRole("listitem").filter({ has: page.getByRole("checkbox", { name: new RegExp(`Pilih jurnal ${demo.healthyStudent}`, "i") }) });
  await approvalCard.getByRole("button", { name: new RegExp(`Buka jurnal ${demo.healthyStudent}`, "i") }).click();
  await approvalCard.getByRole("button", { name: /setujui jurnal/i }).click();
  await expect(approvalCard).toHaveCount(0);

  const rejectedCard = page.getByRole("listitem").filter({ has: page.getByRole("checkbox", { name: new RegExp(`Pilih jurnal ${demo.rejectedStudent}`, "i") }) });
  await rejectedCard.getByRole("button", { name: new RegExp(`Buka jurnal ${demo.rejectedStudent}`, "i") }).click();
  await rejectedCard.getByRole("button", { name: /tolak dan kirim alasan/i }).click();
  await page.getByLabel("Alasan penolakan").fill("Tambahkan rincian kegiatan.");
  await page.getByRole("button", { name: /tolak jurnal/i }).click();
  await expect(rejectedCard).toHaveCount(0);
  checkBrowser();
});

test("Student revises a rejected journal and opens portfolio", async ({ page }) => {
  requireCredentials("Student", credentials.student);
  const checkBrowser = await installBrowserPolicyGuard(page);
  await login(page, credentials.student, "/student");

  await expect(page.getByRole("heading", { name: /apa yang harus saya kerjakan sekarang/i })).toBeVisible();
  await expect(page.getByRole("main").getByRole("alert")).toContainText(/ditolak|alasan|tambahkan rincian/i);
  await page.getByLabel("Apa yang kamu kerjakan hari ini?").fill("Menambahkan rincian kegiatan setelah revisi.");
  await page.getByLabel("Tambah foto jurnal").setInputFiles({
    name: "journal-photo.png",
    mimeType: "image/png",
    buffer: journalPhotoPng,
  });
  await expect(page.getByAltText(/pratinjau journal-photo\.png/i)).toBeVisible();
  await page.getByRole("button", { name: /kirim untuk ditinjau/i }).click();
  await expect(page.getByText(/terkirim|menunggu/i)).toBeVisible();

  await page.getByRole("link", { name: /portofolio/i }).click();
  await expect(page).toHaveURL(/\/student\/portofolio/);
  await expect(page.getByRole("heading", { name: /portofolio/i })).toBeVisible();

  if (demo.certificateCode) {
    await page.goto(`/verify/${encodeURIComponent(demo.certificateCode)}`);
    await expect(page.getByText(/sertifikat|terverifikasi/i)).toBeVisible();
    await expect(page.getByText(/NISN|email|telepon|phone/i)).toHaveCount(0);
  }
  checkBrowser();
});
