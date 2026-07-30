// Keep this predicate aligned with AccountEndpoints.GetSafeReturnUrl in the backend.
// Only the invalid-value fallback intentionally differs: null here, /account/continue there.
export function getSafeLocalReturnUrl(
  value: string | null | undefined,
): string | null {
  return value?.startsWith("/") &&
    !value.startsWith("//") &&
    !value.includes("\\") &&
    !Array.from(value).some((character) => {
      const codePoint = character.codePointAt(0) ?? 0;
      return codePoint <= 0x1f || (codePoint >= 0x7f && codePoint <= 0x9f);
    })
    ? value
    : null;
}
