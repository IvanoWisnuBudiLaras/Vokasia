import { describe, expect, test } from "bun:test";
import { journalDraftKey, parseJournalDraft, serializeJournalDraft } from "./journalDraft";

describe("journal draft", () => {
  test("scopes each draft to the signed-in user and slot", () => {
    expect(journalDraftKey("tenant-a:user-a", "slot-1")).toBe(
      "vokasia:journal-draft:v1:tenant-a:user-a:slot-1",
    );
    expect(journalDraftKey("tenant-a:user-b", "slot-1")).not.toBe(
      journalDraftKey("tenant-a:user-a", "slot-1"),
    );
  });

  test("restores only valid and currently available competencies", () => {
    const now = Date.UTC(2026, 6, 27);
    const stored = serializeJournalDraft("Belajar inspeksi", ["c-1", "removed", "c-1"], now);

    expect(parseJournalDraft(stored, new Set(["c-1", "c-2"]), now)).toEqual({
      version: 1,
      text: "Belajar inspeksi",
      competencyIds: ["c-1"],
      savedAt: now,
    });
  });

  test("rejects malformed and stale data", () => {
    const now = Date.UTC(2026, 6, 27);
    expect(parseJournalDraft("{", new Set(), now)).toBeNull();
    expect(
      parseJournalDraft(
        JSON.stringify({ version: 1, text: "lama", competencyIds: [], savedAt: now - 8 * 24 * 60 * 60 * 1000 }),
        new Set(),
        now,
      ),
    ).toBeNull();
  });
});
