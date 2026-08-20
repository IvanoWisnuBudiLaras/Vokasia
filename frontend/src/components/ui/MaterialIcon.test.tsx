import { expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { MaterialIcon } from "./MaterialIcon";

test("MaterialIcon uses Material Symbols Rounded and hides decorative icons", () => {
  const html = renderToStaticMarkup(<MaterialIcon name="warning" decorative />);
  expect(html).toContain("material-symbols-rounded:warning");
  expect(html).toContain('aria-hidden="true"');
  expect(html).not.toContain("lucide");
});

test("MaterialIcon exposes an accessible name for meaningful icons", () => {
  const html = renderToStaticMarkup(<MaterialIcon name="journal" label="Jurnal" />);
  expect(html).toContain('aria-label="Jurnal"');
  expect(html).toContain('role="img"');
});
