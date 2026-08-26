export const RICH_TEXT_MAX_CHARACTERS = 500;

export function richTextPlainText(input: string): string {
  if (!input) return "";
  if (typeof DOMParser !== "undefined") {
    const doc = new DOMParser().parseFromString(input, "text/html");
    return doc.body.textContent?.trim() || "";
  }
  return input
    .replace(/<(script|style)\b[^>]*>[\s\S]*?<\/\1\s*>/gi, "")
    .replace(/<[^>]*>/g, "")
    .replace(/[ \t]{2,}/g, " ")
    .trim();
}

export function parseRichTextDocument(input: string): { html: string } | null {
  if (!input) return null;
  return { html: input };
}
