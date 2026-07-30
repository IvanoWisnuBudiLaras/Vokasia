FROM oven/bun:1 AS build
WORKDIR /app
COPY package.json bun.lock* bun.lockb* ./
RUN bun install --frozen-lockfile || bun install
COPY . .
RUN bun run build

FROM oven/bun:1-slim AS final
WORKDIR /app
ENV NODE_ENV=production
# GAP ditemukan+ditambal Gate M0 (DECISIONS.md D23): Docker otomatis set HOSTNAME env var ke
# container ID; server.js standalone Next.js baca HOSTNAME itu utk bind address KALAU tak
# di-override -> server cuma listen di alamat container-id itu, BUKAN 0.0.0.0/loopback. Akibat:
# healthcheck (curl/fetch ke localhost DARI DALAM container sendiri) selalu gagal connect,
# meski port-mapping host->container tetap kebetulan jalan (proxy Docker target IP asli
# container, yg sama dgn yg dipakai server). Override eksplisit -> bind ke semua interface,
# termasuk loopback, healthcheck internal bisa connect.
ENV HOSTNAME="0.0.0.0"
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public
EXPOSE 3000
CMD ["bun", "server.js"]
