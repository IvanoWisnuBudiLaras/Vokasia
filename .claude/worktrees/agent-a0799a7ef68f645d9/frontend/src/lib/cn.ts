/** Gabungkan className, buang falsy — util dipakai semua komponen inti. */
export function cn(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(" ");
}
