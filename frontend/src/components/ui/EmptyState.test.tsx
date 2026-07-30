import { expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { EmptyState } from "./EmptyState";

test("EmptyState renders useful copy and a default refresh action", () => {
  const html = renderToStaticMarkup(
    <EmptyState
      title="Belum ada jurnal"
      description="Jurnal harian akan muncul setelah periode dimulai."
    />,
  );

  expect(html).toContain("Belum ada jurnal");
  expect(html).toContain("Jurnal harian akan muncul setelah periode dimulai.");
  expect(html).toContain("Periksa lagi");
  expect(html).toContain('type="button"');
  expect(html).toContain("text-base");
  expect(html).not.toContain("text-sm");
});

test("EmptyState replaces its default action with a supplied action", () => {
  const html = renderToStaticMarkup(
    <EmptyState
      title="Belum ada jurnal"
      description="Mulai dari tugas pertama."
      action={<a href="/student/journal/new">Buat jurnal</a>}
    />,
  );

  expect(html).toContain('href="/student/journal/new"');
  expect(html).toContain("Buat jurnal");
  expect(html).not.toContain("Periksa lagi");
});
