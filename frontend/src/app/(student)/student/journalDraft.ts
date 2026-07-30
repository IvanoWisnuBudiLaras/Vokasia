const DRAFT_VERSION = 1;
const MAX_TEXT_LENGTH = 500;
const MAX_COMPETENCIES = 5;
const MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;

export interface JournalDraft {
  version: typeof DRAFT_VERSION;
  text: string;
  competencyIds: string[];
  savedAt: number;
}

export function journalDraftKey(draftScope: string, slotId: string): string {
  return `vokasia:journal-draft:v${DRAFT_VERSION}:${draftScope}:${slotId}`;
}

export function parseJournalDraft(
  value: string | null,
  allowedCompetencyIds: ReadonlySet<string>,
  now = Date.now(),
): JournalDraft | null {
  if (!value) return null;

  try {
    const parsed: unknown = JSON.parse(value);
    if (
      typeof parsed !== "object" ||
      parsed === null ||
      !("version" in parsed) ||
      parsed.version !== DRAFT_VERSION ||
      !("text" in parsed) ||
      typeof parsed.text !== "string" ||
      !("competencyIds" in parsed) ||
      !Array.isArray(parsed.competencyIds) ||
      !parsed.competencyIds.every((id) => typeof id === "string") ||
      !("savedAt" in parsed) ||
      typeof parsed.savedAt !== "number" ||
      !Number.isFinite(parsed.savedAt) ||
      now - parsed.savedAt > MAX_AGE_MS
    ) {
      return null;
    }

    return {
      version: DRAFT_VERSION,
      text: parsed.text.slice(0, MAX_TEXT_LENGTH),
      competencyIds: [...new Set(parsed.competencyIds)]
        .filter((id) => allowedCompetencyIds.has(id))
        .slice(0, MAX_COMPETENCIES),
      savedAt: parsed.savedAt,
    };
  } catch {
    return null;
  }
}

export function serializeJournalDraft(text: string, competencyIds: string[], savedAt = Date.now()): string {
  return JSON.stringify({
    version: DRAFT_VERSION,
    text: text.slice(0, MAX_TEXT_LENGTH),
    competencyIds: [...new Set(competencyIds)].slice(0, MAX_COMPETENCIES),
    savedAt,
  } satisfies JournalDraft);
}
