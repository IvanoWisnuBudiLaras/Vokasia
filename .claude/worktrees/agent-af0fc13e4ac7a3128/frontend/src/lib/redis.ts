import Redis from "ioredis";

/**
 * VOK-H2-E3 — satu koneksi Redis proses (BFF session store). `REDIS_URL` baru (konvensi Node
 * standar, bukan `Redis__Connection` milik backend .NET) — nilai sama secara fisik (redis:6379
 * di docker-compose, localhost:6379 dev lokal); didaftarkan terpisah di docker-compose.yml
 * (service frontend) + .env.example krn Next.js tidak membaca env var gaya ASP.NET Core.
 */
let client: Redis | undefined;

export function getRedis(): Redis {
  client ??= new Redis(process.env.REDIS_URL ?? "redis://localhost:6379", {
    maxRetriesPerRequest: 3,
    lazyConnect: false,
  });
  return client;
}
