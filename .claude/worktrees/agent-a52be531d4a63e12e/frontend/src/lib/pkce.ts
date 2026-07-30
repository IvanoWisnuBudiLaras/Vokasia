import { createHash, randomBytes } from "node:crypto";

export interface PkcePair {
  verifier: string;
  challenge: string;
}

function base64url(input: Buffer): string {
  return input.toString("base64url");
}

/** VOK-H2-E3 handleLogin — code_verifier (RFC 7636: 43-128 char unreserved) + S256 challenge. */
export function generatePkce(): PkcePair {
  const verifier = base64url(randomBytes(32)); // 32 byte -> 43 char base64url, dalam rentang RFC 7636.
  const challenge = base64url(createHash("sha256").update(verifier).digest());
  return { verifier, challenge };
}

export function generateState(): string {
  return base64url(randomBytes(16));
}
