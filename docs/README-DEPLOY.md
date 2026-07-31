# Vokasia VPS 1-Host Deployment Runbook

## Overview

This runbook describes the single-node hosting topology and container layout for the Vokasia PKL platform. The deployment targets a single Virtual Private Server (VPS) matching **NFR-REL-01** (1 VPS deployment) and **NFR-MNT-04** (reproducible clean boot via Docker Compose).

## Architecture & Container Layout

The system spans 7 service containers arranged on a single private virtual bridge network inside Docker Compose:

1. **`vokasia-frontend`**: Next.js App Router (PPR enabled) running on Oven Bun 1-slim. Listens internally on port 3000.
2. **`vokasia-api`**: ASP.NET Core Web API on .NET 10 LTS. Hosts Identity, OpenIddict OAuth server, and public endpoints. Mapped to host port `5000`.
3. **`vokasia-worker`**: .NET 10 background daemon. Hosts MassTransit message handlers (async queue consumers for Certs, emails, and streaks) and Hangfire job dashboard.
4. **`postgres`**: Postgres 17 database storing all platform entities and the transactional outbox. Mapped to host port `5432` (firewalled to localhost).
5. **`redis`**: Redis 7 caching and BFF HTTP state store. Mapped to host port `6379`.
6. **`rabbitmq`**: RabbitMQ 3 brokers handling all messaging and DLQ. Mapped to host ports `5672` and `15672` (mgmt console).
7. **`minio`**: MinIO S3 object store holding school certificates, student daily photos, and counselor uploads. Mapped to host ports `9000` (API) and `9001` (console).

## Clean State Spin-Up

`docker-compose.yml` is the local-development profile. Production must use the override below so
the API/worker run in `Production`, OAuth redirects use the real HTTPS origin, and Data Protection
keys/certificates persist across restarts.

To deploy the stack on a fresh VPS from scratch:

1. **Prepare Host Environment**:
   - Install **Docker Engine & Docker Compose** (minimum Docker v24+).
   - Put a TLS reverse proxy (Nginx/Caddy/Traefik) in front of the private frontend/API network;
     the production override binds frontend/API only to host loopback (`127.0.0.1:3000/5000`).
   - Keep the certificate files outside git (the examples below use `./secrets/openiddict`).

2. **Configure Environment Variables**:
   Prepare a secure `.env` file at the repository root containing:
   ```env
   # Database Configurations
   POSTGRES_USER=vokasia
   POSTGRES_PASSWORD=secure_postgres_passxx22
   POSTGRES_DB=vokasia
   
   # OIDC BFF secret (>= 32 random characters)
   OIDC_BFF_CLIENT_SECRET=<random-secret>
   
   # MinIO Access Credentials
   MINIO_ROOT_USER=vokasia
   MINIO_ROOT_PASSWORD=secure_minio_root_key233
   
   # Internal service URL (server-to-server only)
   API_INTERNAL_URL=http://api:8080
   # Browser-visible HTTPS origins (no localhost in production)
   API_PUBLIC_URL=https://api.example.id
   FRONTEND_PUBLIC_URL=https://app.example.id
   Frontend__PublicUrl=https://app.example.id

   # OpenIddict certificate passwords and trusted proxy network
   DATAPROTECTION_CERTIFICATE_PASSWORD=<secret>
   OPENIDDICT_ENCRYPTION_CERTIFICATE_PASSWORD=<secret>
   OPENIDDICT_SIGNING_CERTIFICATE_PASSWORD=<secret>
   FORWARDED_HEADERS_KNOWN_IP_NETWORKS=172.16.0.0/12
   FORWARDED_HEADERS_ALLOWED_HOSTS=app.example.id,api.example.id
   ```

   Store `secrets/openiddict/encryption.pfx`, `signing.pfx`, and `dataprotection.pfx` with
   restrictive permissions. Use distinct RSA certificates; never use the HTTPS certificate for
   OpenIddict or Data Protection.

3. **Launch Containers**:
   ```bash
   docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
   ```
   Verify all containers are healthy:
   ```bash
   docker compose ps
   ```

4. **Seed Database Schema and Demo Data**:
   Execute the migration and seeder inside the API container:
   ```bash
   # Demo seeding is Development-only and is intentionally rejected by the production binary.
   # Provision real tenants through the SuperAdmin flow or run the command against the local
   # development profile only:
   docker compose -f docker-compose.yml up -d --build
   docker compose exec api dotnet Vokasia.Api.dll seed demo
   ```

---

## Daily Backup and Retention Policy

To satisfy **NFR-REL-02**, a daily database dump script is installed via standard system `cron` on the VPS host machine.

### Backup Script (`/opt/vokasia/scripts/backup-db.sh`)

```bash
#!/usr/bin/env bash
set -euo pipefail

# Output Directory
BACKUP_DIR="/var/backups/vokasia"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="${BACKUP_DIR}/vokasia_db_${TIMESTAMP}.sql.gz"

mkdir -p "${BACKUP_DIR}"

# Run pg_dump through docker inspect
docker compose exec -t postgres pg_dump -U vokasia -d vokasia | gzip > "${BACKUP_FILE}"

# Enforce 14-day retention (delete older than 14 days)
find "${BACKUP_DIR}" -name "vokasia_db_*.sql.gz" -mtime +14 -delete

echo "Backup complete: ${BACKUP_FILE}"
```

### Installation

Add to host crontab (`crontab -e`):
```cron
# Every night at 02:30 AM local time
30 2 * * * /opt/vokasia/scripts/backup-db.sh >> /var/log/vokasia-backup.log 2>&1
```

---

## Disaster Recovery & Database Restore

To test or perform a full restore onto a blank Postgres database instance:

1. **Flush active connections and drop DB** (Caution: destructive):
   ```bash
   docker compose exec postgres psql -U vokasia -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'vokasia' AND pid <> pg_backend_pid();"
   docker compose exec postgres dropdb -U vokasia vokasia --if-exists
   docker compose exec postgres createdb -U vokasia vokasia
   ```

2. **Decompress and Stream the backup file back into Postgres**:
   ```bash
   gunzip -c /var/backups/vokasia/vokasia_db_20260723_023000.sql.gz | docker compose exec -T postgres psql -U vokasia -d vokasia
   ```

3. **Verify Restoration**:
   Restart the services to verify the state recovery:
   ```bash
   docker compose restart api worker
   ```
