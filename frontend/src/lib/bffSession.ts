import { createHmac, randomUUID, timingSafeEqual } from "node:crypto";
import { getRedis } from "./redis";
import type { Role } from "./session";

export const SESS_COOKIE_NAME = "vok_sess";

const SESS_PREFIX = "sess:";
const USER_SESSIONS_PREFIX = "user-sessions:";
const PKCE_PREFIX = "pkce:";
const PKCE_TTL_SECONDS = 300; // 5 menit — cukup utk round-trip authorize->login form->callback.
const SESSION_TTL_SECONDS = 60 * 60 * 24 * 14; // 14 hari, cermin OpenIddictSetup.cs SetRefreshTokenLifetime.

export interface BffSessionUser {
  id: string;
  name: string;
  role: Role;
  tenantId: string | null;
}

export interface BffSession {
  accessToken: string;
  accessExp: number; // epoch ms
  refreshToken: string;
  user: BffSessionUser;
  /**
   * VOK-H6-E3 §2: non-null SELAMA SuperAdmin sedang impersonasi user lain — token/identitas SA
   * ASLI di-stash di sini (BUKAN dibuang), accessToken/refreshToken/user di level atas ditimpa
   * sementara dgn identitas TARGET. EndImpersonation mengembalikan field-field level atas dari
   * sini lalu menghapus field ini — sessionId (cookie vok_sess) TIDAK PERNAH berganti selama
   * seluruh siklus (start->end), hanya ISI record Redis-nya yang ditukar-tukar.
   */
  impersonation?: {
    originalAccessToken: string;
    originalAccessExp: number;
    originalRefreshToken: string;
    originalUser: BffSessionUser;
  };
}

function secret(): string {
  const s = process.env.SESSION_SECRET;
  if (!s || s.length < 16) {
    throw new Error(
      "SESSION_SECRET belum diset atau terlalu pendek (lihat .env.example) — wajib utk tanda tangan cookie sesi."
    );
  }
  return s;
}

function sign(value: string): string {
  return createHmac("sha256", secret()).update(value).digest("base64url");
}

/**
 * cookie `vok_sess` = "{sessionId}.{hmac(sessionId)}" — HMAC cegah tebak/rekayasa sessionId dari
 * luar (defense-in-depth; Redis tetap satu-satunya sumber kebenaran sesi, cookie yang valid tapi
 * mengarah ke sessionId yang sudah dihapus/tidak ada tetap ditolak oleh getSessionData()==null).
 */
export function encodeSessCookie(sessionId: string): string {
  return `${sessionId}.${sign(sessionId)}`;
}

export function decodeSessCookie(raw: string | undefined | null): string | null {
  if (!raw) return null;
  const idx = raw.lastIndexOf(".");
  if (idx <= 0) return null;

  const sessionId = raw.slice(0, idx);
  const mac = raw.slice(idx + 1);
  const expected = sign(sessionId);

  const a = Buffer.from(mac);
  const b = Buffer.from(expected);
  if (a.length !== b.length || !timingSafeEqual(a, b)) return null;

  return sessionId;
}

export async function savePkce(state: string, payload: string): Promise<void> {
  await getRedis().set(`${PKCE_PREFIX}${state}`, payload, "EX", PKCE_TTL_SECONDS);
}

/** Sekali pakai — dihapus begitu dibaca (anti replay state). */
export async function consumePkce(state: string): Promise<string | null> {
  const redis = getRedis();
  const payload = await redis.get(`${PKCE_PREFIX}${state}`);
  if (payload) await redis.del(`${PKCE_PREFIX}${state}`);
  return payload;
}

export async function createSession(data: BffSession): Promise<string> {
  const sessionId = randomUUID();
  const redis = getRedis();
  await redis.set(`${SESS_PREFIX}${sessionId}`, JSON.stringify(data), "EX", SESSION_TTL_SECONDS);
  await redis.sadd(`${USER_SESSIONS_PREFIX}${data.user.id}`, sessionId);
  await redis.expire(`${USER_SESSIONS_PREFIX}${data.user.id}`, SESSION_TTL_SECONDS);
  return sessionId;
}

export async function getSessionData(sessionId: string): Promise<BffSession | null> {
  const raw = await getRedis().get(`${SESS_PREFIX}${sessionId}`);
  return raw ? (JSON.parse(raw) as BffSession) : null;
}

export async function updateSessionTokens(
  sessionId: string,
  accessToken: string,
  accessExp: number,
  refreshToken: string
): Promise<void> {
  const existing = await getSessionData(sessionId);
  if (!existing) return;
  const updated: BffSession = { ...existing, accessToken, accessExp, refreshToken };
  await getRedis().set(`${SESS_PREFIX}${sessionId}`, JSON.stringify(updated), "EX", SESSION_TTL_SECONDS);
}

export async function deleteSession(sessionId: string): Promise<BffSession | null> {
  const redis = getRedis();
  const data = await getSessionData(sessionId);
  await redis.del(`${SESS_PREFIX}${sessionId}`);
  if (data) await redis.srem(`${USER_SESSIONS_PREFIX}${data.user.id}`, sessionId);
  return data;
}

/**
 * VOK-H6-E3 §2 StartImpersonation (sisi BFF) — stash identitas SA ASLI (level atas record
 * SEKARANG, SEBELUM ditimpa) ke field `impersonation`, lalu timpa accessToken/refreshToken/user
 * dgn identitas target. sessionId/cookie vok_sess TIDAK berubah (1 record Redis yang sama diedit
 * di tempat) — proxy.ts/fetcher.ts lain tak perlu tahu apa pun berubah, mereka tetap baca
 * getSessionData(sessionId) seperti biasa dan dapat identitas TARGET secara transparan.
 */
export async function startImpersonation(
  sessionId: string,
  target: { accessToken: string; accessExp: number; user: BffSessionUser }
): Promise<BffSession | null> {
  const existing = await getSessionData(sessionId);
  if (!existing) return null;

  const updated: BffSession = {
    accessToken: target.accessToken,
    accessExp: target.accessExp,
    refreshToken: "", // grant impersonation TIDAK menerbitkan refresh token (short-lived by design, lihat DECISIONS.md D39) - habis 15 mnt, EndImpersonation wajib dipanggil sebelum itu utk kembali normal.
    user: target.user,
    impersonation: {
      originalAccessToken: existing.accessToken,
      originalAccessExp: existing.accessExp,
      originalRefreshToken: existing.refreshToken,
      originalUser: existing.user,
    },
  };
  await getRedis().set(`${SESS_PREFIX}${sessionId}`, JSON.stringify(updated), "EX", SESSION_TTL_SECONDS);
  return updated;
}

/** VOK-H6-E3 §2 EndImpersonation (sisi BFF) — kembalikan field level atas dari stash, buang `impersonation`. Null bila memang tidak sedang impersonasi (caller wajib cek dulu). */
export async function endImpersonation(sessionId: string): Promise<BffSession | null> {
  const existing = await getSessionData(sessionId);
  if (!existing?.impersonation) return null;

  const restored: BffSession = {
    accessToken: existing.impersonation.originalAccessToken,
    accessExp: existing.impersonation.originalAccessExp,
    refreshToken: existing.impersonation.originalRefreshToken,
    user: existing.impersonation.originalUser,
  };
  await getRedis().set(`${SESS_PREFIX}${sessionId}`, JSON.stringify(restored), "EX", SESSION_TTL_SECONDS);
  return restored;
}

/** Reuse detection VOK-H2-E3 AC: "seluruh keluarga sesi tercabut" — hapus SEMUA sesi user ini. */
export async function revokeAllSessionsForUser(userId: string): Promise<string[]> {
  const redis = getRedis();
  const key = `${USER_SESSIONS_PREFIX}${userId}`;
  const sessionIds = await redis.smembers(key);
  if (sessionIds.length > 0) {
    await redis.del(...sessionIds.map((id) => `${SESS_PREFIX}${id}`));
  }
  await redis.del(key);
  return sessionIds;
}
