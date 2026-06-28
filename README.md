# m5rcel's awesome file hoster

A self-hosted, Material You-inspired file hosting platform built entirely with .NET 10, ASP.NET Core, Razor components, PostgreSQL, Redis, and local filesystem storage.

**Production URL:** `https://files.index.sarl`  
**Tagline:** Upload it. Compress it. Share it.

## Features

- ASP.NET Core Identity registration, login, secure cookies, lockout, roles, and persistent Data Protection keys
- Bounded streaming uploads with generated paths, signature-based MIME detection, SHA-256 duplicate detection, quotas, and blocked executable extensions
- Local-only originals, processed files, thumbnails, temporary files, and quarantine storage
- Durable Redis processing queue with database recovery after worker interruption
- Magick.NET image/WebP optimization, FFmpeg video/audio processing, gifsicle GIF optimization, and safe archive handling without extraction
- Optional ClamAV streaming scan and quarantine
- Public galleries, profiles, NSFW blur/reveal, media previews, downloads, reports, and owner controls
- Moderator/admin dashboard, file/user/report APIs, storage/job views, and audit logging
- Rate limiting, antiforgery validation, authorization policies, security headers, health checks, and non-root containers
- Responsive navigation rail/bottom navigation, light/dark themes, upload progress, loading/empty states, chips, cards, and motion

## Screenshots

Add deployment screenshots here after branding and production content are configured:

- Landing page
- Upload flow
- Public gallery
- File detail
- Admin dashboard

## Architecture

```text
src/
├── M5FileHost.Core/            domain entities, enums, options, contracts
├── M5FileHost.Infrastructure/  EF Core, PostgreSQL, Redis, storage, scanning, processing
├── M5FileHost.Web/             ASP.NET Core host, Razor UI, Identity, APIs
└── M5FileHost.Worker/          durable background processing and recovery
tests/
└── M5FileHost.Tests/           signature detection and filesystem safety tests
```

PostgreSQL stores metadata and Identity records. Redis stores processing messages. File bytes never enter PostgreSQL or external object storage.

## Requirements

Production:

- Docker Engine with Compose v2
- Host Caddy binary (not included as a Compose service)
- DNS `files.index.sarl` pointing to the server
- Ports 80/443 reachable by Caddy
- At least 2 GB RAM without ClamAV; 4 GB or more with ClamAV/video processing

Development:

- .NET 10 SDK
- PostgreSQL 17+
- Redis 7+
- FFmpeg, gifsicle, and 7-Zip for processing tests/manual runs

## Production setup

Generate Base64URL-style passwords without semicolons because the PostgreSQL password is interpolated into a connection string:

```bash
openssl rand -base64 36 | tr '+/' '-_' | tr -d '=\n'
```

Then configure and start:

```bash
cp .env.example .env
nano .env

mkdir -p data/{uploads,keys,postgres,redis,clamav}
sudo chown -R 1654:1654 data/uploads data/keys
sudo chmod 700 data/uploads data/keys

docker compose up -d --build
docker compose ps
docker compose logs -f app worker
```

Files whose names start with `.` are required. If deploying without Git, copy
the repository directory itself (for example, `rsync -a ./ user@host:~/mafh.net/`)
instead of using a shell wildcard such as `scp *`, which omits `.env.example`,
`.dockerignore`, and `.gitignore`. Never copy or commit the secret `.env` file;
create it on the server from `.env.example`.

The app applies committed EF Core migrations on startup and seeds the owner only when all three `OWNER_*` variables are present. After the first successful login, blank `OWNER_PASSWORD` in `.env` and recreate the app container so the bootstrap password is no longer present in its environment:

```bash
docker compose up -d --force-recreate app
```

The owner command also applies any pending migrations before seeding:

```bash
docker compose exec app dotnet M5FileHost.Web.dll seed-owner
```

This is normally unnecessary because `Database__MigrateOnStartup=true`. For operational migrations, the safe path is to let the versioned app image apply its embedded migrations during startup.

### Optional ClamAV

Set `ENABLE_CLAMAV=true`, then start the profile:

```bash
docker compose --profile malware-scan up -d
```

ClamAV needs time and memory to download signatures before scans succeed. When enabled, scanner errors fail processing rather than silently accepting an unscanned file.

## Caddy

Copy the repository `Caddyfile` block into the host configuration, then run:

```bash
sudo mkdir -p /var/log/caddy
sudo chown caddy:caddy /var/log/caddy
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Only `127.0.0.1:3055` is published by Docker. PostgreSQL and Redis have no host ports.

## Local development

Start PostgreSQL and Redis, adjust development connection strings if needed, then:

```bash
export ConnectionStrings__Postgres='Host=localhost;Port=5432;Database=filehost;Username=filehost;Password=YOUR_LOCAL_PASSWORD'
export ConnectionStrings__Redis='localhost:6379,abortConnect=false'
dotnet restore M5FileHost.slnx
dotnet tool install --global dotnet-ef --version 10.0.9
dotnet ef database update --project src/M5FileHost.Infrastructure --startup-project src/M5FileHost.Worker
dotnet run --project src/M5FileHost.Worker
dotnet run --project src/M5FileHost.Web
```

Development uses HTTP-compatible cookies. Production always requires secure HTTPS cookies through Caddy.

## Tests and verification

```bash
dotnet build M5FileHost.slnx -c Release
dotnet test M5FileHost.slnx -c Release --no-build
```

The tests verify magic-byte detection, compound archive extensions, UTF-8/binary classification, path traversal rejection, streaming hashes, upload limits, and partial-file cleanup.

After deployment, verify:

```bash
curl -fsS http://127.0.0.1:3055/health
curl -I https://files.index.sarl
docker compose logs --since=10m app worker
```

Register a non-admin account and exercise public, unlisted, private, NSFW, duplicate, oversized, and blocked-extension uploads. Confirm private files fail from a signed-out browser and admin actions appear in `/admin/audit-log`.

## Storage layout

```text
data/uploads/
├── originals/YYYY/MM/DD/{id}-original.ext
├── processed/YYYY/MM/DD/{id}-processed.ext
├── thumbnails/YYYY/MM/DD/{id}.webp
├── temp/
└── quarantine/YYYY/MM/DD/{id}.ext
```

Original filenames are metadata only. Paths use server-generated GUIDs and are canonicalized under the configured root. Downloads are served through authorized ASP.NET endpoints with `nosniff`, safe disposition, and range support—not as public static files.

Images are stripped and converted to WebP with thumbnails. GIFs use gifsicle and FFmpeg thumbnails. Video uses H.264/AAC MP4 with bounded resolution and processing time. Audio uses Opus. Archives are never extracted or executed. Unsupported files remain available as originals.

## Backups

Stop new writes or place the service in maintenance mode for a fully consistent file/database pair.

PostgreSQL:

```bash
mkdir -p backups
docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc > "backups/filehost-$(date +%F).dump"
```

Files and Data Protection keys:

```bash
sudo tar -C data -czf "backups/filehost-files-$(date +%F).tar.gz" uploads keys
```

Restore PostgreSQL with `pg_restore --clean --if-exists` into an empty/maintenance database, then restore `uploads` and `keys` before starting the app. Losing `keys` invalidates existing sessions and reset tokens but does not lose accounts or files.

## Updating

```bash
git pull --ff-only
docker compose build --pull app worker
docker compose up -d
docker compose logs -f --since=5m app worker
```

Back up before upgrades. Migrations are forward-only in production; rollback means restoring the matching database and file backup with the prior image.

## Security and operator checklist

- Replace every `CHANGE_ME` value and use unique secrets.
- Review `/terms`, `/privacy`, `/rules`, and `/dmca` with qualified counsel; the included text is an operational template, not legal advice.
- Configure `SMTP_HOST`, `SMTP_FROM`, and any required SMTP credentials to enable password-reset delivery. Responses remain non-enumerating when email is unavailable.
- Keep registration closed until moderation contacts, retention policy, backup monitoring, and abuse response are ready.
- Enable ClamAV for public registration and monitor scanner/worker failures.
- Restrict filesystem permissions and never expose `data/uploads` through Caddy.
- Rotate the owner password immediately and leave the bootstrap password blank afterward.
- Set host/container CPU, memory, and disk quotas appropriate to the deployment. Processing has time limits, but Docker resource limits remain operator-specific.

## Troubleshooting

- **Docker sends a very large build context or publish fails with `NETSDK1064`:** confirm `.dockerignore` exists on the server, remove any copied `bin`/`obj` directories, then rebuild with `docker compose build --no-cache app worker`.
- **App is unhealthy:** inspect `docker compose logs app postgres`; health checks require a live database.
- **Worker jobs stay pending:** inspect `docker compose logs worker redis`; pending database rows are republished every five minutes.
- **Upload permission denied:** `sudo chown -R 1654:1654 data/uploads data/keys`.
- **Video/GIF processing failed:** verify FFmpeg/gifsicle are present in the runtime image and inspect the file's processing error/admin jobs page.
- **Login loops over HTTP:** Production cookies require HTTPS. Use Caddy or run with `ASPNETCORE_ENVIRONMENT=Development` locally.
- **ClamAV timeouts:** wait for signature initialization, increase memory, and inspect `docker compose logs clamav`.
- **Caddy returns 502:** verify `curl http://127.0.0.1:3055/health` and confirm the app port binding.

## License

Add the license appropriate for your deployment and distribution requirements. Magick.NET is Apache-2.0 licensed; FFmpeg codec licensing depends on the distributed Alpine packages and intended jurisdiction/use.
