#!/usr/bin/env bash
#
# Build and deploy Swan Content Publisher on a Linux server.
# Run ON the server, from a checkout of this repository, as a user that can sudo.
#
#   cd /opt/swan/src && ./deploy/deploy.sh
#
# Requires: dotnet SDK (10.x), node + npm (22.x), rsync, and the swan-api systemd service
# created from deploy/systemd/swan-api.service. See docs/deploy-ubuntu.md for first-time setup.

set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_DIR="${APP_DIR:-/opt/swan/app}"
SERVICE="${SERVICE:-swan-api}"
PUBLISH_DIR="$(mktemp -d)"

echo "==> Repository: $REPO_DIR"
cd "$REPO_DIR"

if [ -d .git ]; then
  echo "==> Pulling latest"
  git pull --ff-only
fi

echo "==> Building Angular client"
pushd src/web/Swan.Web >/dev/null
npm ci
npm run build
popd >/dev/null

echo "==> Bundling client into the API's wwwroot"
rm -rf src/Swan.Api/wwwroot
mkdir -p src/Swan.Api/wwwroot
cp -r src/web/Swan.Web/dist/swan-web/browser/. src/Swan.Api/wwwroot/

echo "==> Publishing API"
dotnet publish src/Swan.Api/Swan.Api.csproj -c Release -o "$PUBLISH_DIR"

echo "==> Installing to $APP_DIR"
sudo mkdir -p "$APP_DIR"
sudo rsync -a --delete "$PUBLISH_DIR/" "$APP_DIR/"
sudo chown -R swan:swan "$APP_DIR"
rm -rf "$PUBLISH_DIR"

echo "==> Restarting $SERVICE"
sudo systemctl restart "$SERVICE"
sleep 2
sudo systemctl --no-pager --full status "$SERVICE" | head -n 15

echo "==> Done. Health check: curl -fsS http://127.0.0.1:5197/api/health"
