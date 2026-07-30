# TLS edge (opsional) — menutup gate login-di-topologi-prod

Menutup gap yang ditemukan audit: profil prod meng-bind port ke loopback dan **mengandalkan reverse-proxy TLS di depannya**. Service `caddy` (profile `edge`) MENYEDIAKAN proxy itu di dalam compose, sehingga round-trip login browser bisa diuji **end-to-end** di mesin lokal tanpa proxy host terpisah.

> ⚠️ **Ditulis, belum diuji oleh auditor** (sandbox auditor tak punya Docker). Jalankan langkah verifikasi di bawah; kalau ada yang meleset, lihat "Titik bahaya".

## Kunci perubahan vs run prod-mu sebelumnya

Sebelumnya `API_PUBLIC_URL=https://localhost:5000` menunjuk **port API mentah (HTTP)** → login putus. Dengan edge, **frontend dan OIDC berbagi SATU origin HTTPS** (`https://localhost`, Caddy di 443, routing per-path). Jadi:

```
FRONTEND_PUBLIC_URL = https://localhost      (BUKAN :3000)
API_PUBLIC_URL       = https://localhost      (BUKAN :5000)  ← sama dengan frontend
EDGE_PUBLIC_HOST     = localhost
```

`redirect_uri` (`https://localhost/api/auth/callback`) dan `issuer` (`https://localhost`) otomatis ikut — seeder OpenIddict `UpdateAsync` tiap startup (self-healing), jadi tak perlu hapus client manual.

## Jalankan (PowerShell, di host-mu)

```powershell
$env:EDGE_PUBLIC_HOST='localhost'
$env:FRONTEND_PUBLIC_URL='https://localhost'
$env:API_PUBLIC_URL='https://localhost'
$env:OIDC_BFF_CLIENT_SECRET='<secret-panjang>'
$env:DATAPROTECTION_CERTIFICATE_PASSWORD='<pwd>'
$env:OPENIDDICT_ENCRYPTION_CERTIFICATE_PASSWORD='<pwd>'
$env:OPENIDDICT_SIGNING_CERTIFICATE_PASSWORD='<pwd>'
$env:OPENIDDICT_CERTS_DIR='D:\Web\Vokasia\.staging-certs\openiddict'
$env:FORWARDED_HEADERS_ALLOWED_HOSTS='localhost'
# FORWARDED_HEADERS_KNOWN_IP_NETWORKS biarkan default (172.16.0.0/12 → mencakup Caddy di jaringan Docker)

docker compose -f docker-compose.yml -f docker-compose.prod.yml --profile edge up -d --build

# (opsional) percayai CA internal Caddy supaya browser tak memperingatkan cert:
docker exec vokasia-caddy-1 caddy trust
```

## Verifikasi (browser — inilah bukti gate tertutup)

1. `https://localhost/` → landing render (klik-lewati peringatan cert kalau belum `caddy trust`).
2. `https://localhost/login` → klik **"Lanjut ke halaman masuk"**.
3. Harus **redirect ke `https://localhost/connect/authorize` → form `/account/login`** yang ter-render **atas HTTPS**. Ini bukti round-trip OAuth hidup di topologi prod (yang sebelumnya putus).
4. Login akun nyata → callback `https://localhost/api/auth/callback` → dashboard sesuai peran.

Smoke non-browser:
```powershell
curl.exe -k https://localhost/health-frontend  # atau buka https://localhost/ → 200
curl.exe -k -I https://localhost/connect/authorize?client_id=vokasia-bff  # bukan connection-refused
```

## Titik bahaya (kalau gagal)

1. **OpenIddict menolak `/connect/authorize` sebagai non-HTTPS** → berarti API tak mempercayai proto yang diteruskan Caddy. Cek IP container Caddy ada di `FORWARDED_HEADERS_KNOWN_IP_NETWORKS`: `docker network inspect vokasia_default` → kalau subnet bukan 172.16.0.0/12, set env itu ke subnet yang benar.
2. **`invalid redirect_uri`** → seharusnya tak terjadi (seeder self-healing), tapi kalau iya: pastikan `FRONTEND_PUBLIC_URL=https://localhost` benar-benar terbaca API container (`docker exec vokasia-api-1 printenv Frontend__PublicUrl`), lalu `up -d --build` ulang agar re-seed.
3. **Port 443/80 sudah dipakai** proses lain di host → ubah mapping port service `caddy`.
4. **Peringatan cert** wajar untuk `tls internal`; hilang setelah `caddy trust` atau pakai domain nyata + ACME.
5. **Domain nyata**: ganti `EDGE_PUBLIC_HOST` ke domain, HAPUS `tls internal` di `Caddyfile` (Caddy pakai Let's Encrypt), pastikan 80/443 publik & DNS mengarah ke host.

## Kalau tak mau edge di compose

Desain host-proxy tetap didukung: jangan pakai `--profile edge`, jalankan Nginx/Caddy-mu sendiri di host menunjuk ke `127.0.0.1:3000` (frontend) dan `127.0.0.1:5000` (API) dengan routing path yang sama seperti `Caddyfile` ini, dan set `API_PUBLIC_URL`/`FRONTEND_PUBLIC_URL` ke origin HTTPS proxy-mu.
