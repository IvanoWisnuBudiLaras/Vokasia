/** Tag stabil dan pendek; benturan hanya memperluas invalidasi, tidak mencampur cache URL. */
export function publicPortfolioCacheTag(slug: string): string {
  let hash = 2166136261;

  for (let index = 0; index < slug.length; index += 1) {
    hash ^= slug.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return `public-portfolio:${(hash >>> 0).toString(36)}`;
}
