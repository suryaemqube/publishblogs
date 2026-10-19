# Deploying Swan Content Publisher on Ubuntu 24.04

One ASP.NET Core 10 app that also serves the Angular UI, behind Nginx with a Let's Encrypt
certificate. Single origin (`https://publishblogs.emqube.net`), so there is no CORS to configure.

```
Browser ──HTTPS──▶ Nginx (443) ──▶ Kestrel 127.0.0.1:5197 ──▶ /var/lib/swan/data
                                   (systemd: swan-api)         swan.db · documents · keys
```

Substitute your own domain and paths where `publishblogs.emqube.net` and `/opt/swan` appear.

---

## 0. Prerequisites

- Ubuntu 24.04 server with `sudo` access.
- A DNS `A` record: `publishblogs.emqube.net` → your server's public IP. Add it in whichever DNS panel
  manages `emqube.com`. Verify with `dig +short publishblogs.emqube.net` before requesting a certificate.
- Inbound TCP 80 and 443 open (22 for SSH).

---

## 1. Install .NET 10 and Node.js 22

The build runs on the server, so install the **SDK** (which includes the runtime) and Node.

```bash
# --- .NET 10 (Microsoft package repository for Ubuntu 24.04) ---
sudo apt-get update
sudo apt-get install -y wget ca-certificates
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
sudo dpkg -i /tmp/packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
dotnet --version     # expect 10.x

# --- Node.js 22 (NodeSource) ---
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt-get install -y nodejs rsync
node --version       # expect v22.x
```

> If `dotnet-sdk-10.0` is not yet in the Microsoft repo, install it with the official script
> instead: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0` and add
> `~/.dotnet` to `PATH`.

---

## 2. Create the service user and data directories

The app runs as an unprivileged `swan` user. Its data lives outside the code so redeploys never
touch it.

```bash
sudo useradd --system --create-home --home-dir /opt/swan --shell /usr/sbin/nologin swan
sudo mkdir -p /opt/swan/app
sudo mkdir -p /var/lib/swan/data/documents /var/lib/swan/data/keys
sudo chown -R swan:swan /opt/swan /var/lib/swan
```

`/var/lib/swan/data` holds the database, uploaded originals, the Data Protection keys and the JWT
signing key. **Back this whole directory up** (see §10).

---

## 3. Get the code

```bash
sudo mkdir -p /opt/swan/src
sudo chown "$USER":"$USER" /opt/swan/src
git clone <your-repo-url> /opt/swan/src
cd /opt/swan/src
```

(If your repo is private, use a deploy key or a personal access token.)

---

## 4. First publish

`deploy/deploy.sh` builds the Angular client, bundles it into the API's `wwwroot`, publishes the
API, installs it to `/opt/swan/app`, and restarts the service. Run it from the checkout:

```bash
cd /opt/swan/src
chmod +x deploy/deploy.sh
./deploy/deploy.sh
```

The service won't be installed yet on the first run, so the final `systemctl restart` will warn —
that is expected. Continue to §5.

Manual equivalent, if you prefer to see each step:

```bash
cd /opt/swan/src
cd src/web/Swan.Web && npm ci && npm run build && cd ../../..
rm -rf src/Swan.Api/wwwroot && mkdir -p src/Swan.Api/wwwroot
cp -r src/web/Swan.Web/dist/swan-web/browser/. src/Swan.Api/wwwroot/
dotnet publish src/Swan.Api/Swan.Api.csproj -c Release -o /tmp/swan-publish
sudo rsync -a --delete /tmp/swan-publish/ /opt/swan/app/
sudo chown -R swan:swan /opt/swan/app
```

---

## 5. Install the systemd service

```bash
sudo cp /opt/swan/src/deploy/systemd/swan-api.service /etc/systemd/system/swan-api.service
sudo systemctl daemon-reload
sudo systemctl enable --now swan-api
sudo systemctl status swan-api --no-pager
```

Check it is healthy locally:

```bash
curl -fsS http://127.0.0.1:5197/api/health    # {"status":"ok"}
```

The service passes the database, storage, keys and signing-key paths as environment variables
(see the unit file). The app **applies EF migrations on startup**, so the first run creates the
schema.

---

## 6. Nginx reverse proxy and TLS

```bash
sudo apt-get install -y nginx
sudo cp /opt/swan/src/deploy/nginx/swan.conf /etc/nginx/sites-available/swan.conf
sudo ln -sf /etc/nginx/sites-available/swan.conf /etc/nginx/sites-enabled/swan.conf
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx

# TLS certificate (rewrites the config to add HTTPS + redirect)
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d publishblogs.emqube.net
```

Nginx proxies everything to Kestrel, including deep links like `/sites/new`. The app returns
`index.html` for client-side routes and JSON `404` for unknown `/api` paths.

---

## 7. Firewall

```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'
sudo ufw enable
sudo ufw status
```

---

## 8. Create the first admin user

The CLI reads the same configuration as the API (including `Database__ConnectionString`), so run it
as the `swan` user with the same environment. Easiest is to reuse the unit's variables:

```bash
sudo -u swan -H bash -c '
  export ASPNETCORE_ENVIRONMENT=Production
  export Database__ConnectionString="Data Source=/var/lib/swan/data/swan.db"
  dotnet /opt/swan/app/Swan.Api.dll create-user \
    --email you@emqube.com --name "Your Name" --role Administrator
'
```

It prompts for a password twice (minimum 12 characters). Then open `https://publishblogs.emqube.net` and
sign in.

---

## 9. Add your client sites

In the UI: **Sites → Add site**. Enter the WordPress address (the origin that serves `/wp-json`,
e.g. `https://wp.emqube.com`), the username, and an Application Password. Use **Look up post types
and categories** to pick the post type, then **Add site and test**. Drafts are only allowed once the
connection test passes.

---

## 10. Backups (do not skip)

Back up `/var/lib/swan/data` on a schedule. Losing this has real consequences:

| Path | Losing it means |
|------|-----------------|
| `swan.db` | All documents, articles and site configuration are gone |
| `keys/` | **Every stored WordPress Application Password becomes unreadable** and must be re-entered |
| `jwt-signing-key.txt` | All users are signed out (regenerated, but sessions invalidated) |
| `documents/` | Uploaded originals and images are gone |

A simple nightly example (`/etc/cron.daily/swan-backup`):

```bash
#!/bin/sh
set -e
STAMP=$(date +%F)
tar czf /var/backups/swan-$STAMP.tgz -C /var/lib swan
find /var/backups -name 'swan-*.tgz' -mtime +30 -delete
```

For SQLite specifically, prefer `.backup` over copying a live file, or accept the small risk for a
low-write app like this one.

---

## 11. Updating (redeploy)

```bash
cd /opt/swan/src
./deploy/deploy.sh
```

That pulls, rebuilds the client and API, installs to `/opt/swan/app`, and restarts. Migrations run
on startup. A short interruption (~seconds) occurs during restart.

---

## 12. Configuration reference

Set these in the systemd unit (`/etc/systemd/system/swan-api.service`) and `daemon-reload`.

| Setting (env) | Purpose | Recommended value |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Config profile | `Production` |
| `ASPNETCORE_URLS` | Kestrel bind | `http://127.0.0.1:5197` |
| `Database__ConnectionString` | SQLite path | `Data Source=/var/lib/swan/data/swan.db` |
| `Storage__Root` | Uploaded originals | `/var/lib/swan/data/documents` |
| `DataProtection__KeysPath` | Encrypts stored WP passwords | `/var/lib/swan/data/keys` |
| `Auth__SigningKeyFile` | JWT signing key | `/var/lib/swan/data/jwt-signing-key.txt` |
| `AllowedHosts` | Host-header filter | `publishblogs.emqube.net` (use `*` only while testing by IP) |
| `Cors__AllowedOrigins__0` | Extra browser origin | only needed if you split the frontend onto another host |

Never commit secrets. Nothing here needs to be in the repo — pass it through the unit or App
Service settings.

---

## 13. Logs and troubleshooting

```bash
sudo journalctl -u swan-api -f                 # live app logs
sudo journalctl -u swan-api --since "10 min ago"
sudo nginx -t && sudo systemctl reload nginx
curl -fsS http://127.0.0.1:5197/api/health     # is Kestrel up?
```

| Symptom | Likely cause |
|---|---|
| `502 Bad Gateway` from Nginx | Kestrel down — check `journalctl -u swan-api` |
| Health OK by IP, but domain 404/blocked | DNS not pointing at the server, or `AllowedHosts` excludes the host |
| "not configured / password could not be read" on a site | Data Protection `keys/` folder changed or is not readable by `swan` |
| Certbot fails | Port 80 not open, or DNS not yet resolving |
| Sign-in works, then every request 401 | JWT signing key path not writable/persistent |

---

## 14. Notes on scale and alternatives

- **SQLite is fine** for this workload (a handful of writers, low write volume) on a single
  instance. The plan's SQL Server target is only needed once you run multiple app instances or want
  stronger concurrency guarantees. Moving is a connection-string change plus a data copy.
- **Do not run two instances against one SQLite file** on shared storage.
- The same build runs on Azure App Service or in a container later without code changes — set the
  same environment variables and point `DataProtection__KeysPath` and `Storage__Root` at durable
  storage.
