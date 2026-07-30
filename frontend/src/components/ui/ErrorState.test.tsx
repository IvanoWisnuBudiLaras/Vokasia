import { expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { ErrorState } from "./ErrorState";

test("ErrorState is announced and its retry action never submits a parent form", () => {
  const html = renderToStaticMarkup(
    <ErrorState message="Jurnal gagal dimuat." onRetry={() => undefined} />,
  );

  expect(html).toContain('role="alert"');
  expect(html).toContain("Jurnal gagal dimuat.");
  expect(html).toContain("Coba Lagi");
  expect(html).toContain('type="button"');
  expect(html).toContain("h-[var(--tap-min)]");
  expect(html).toContain("text-base");
  expect(html).not.toContain("text-sm");
});
