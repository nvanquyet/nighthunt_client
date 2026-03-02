#!/bin/bash
# =============================================================================
# NightHunt Dedicated Server - Container Entrypoint
# Được gọi bởi Docker ENTRYPOINT khi container start.
# Nhận config từ ENV variables (set bởi backend khi `docker run`).
# =============================================================================
set -e

echo "╔══════════════════════════════════════════╗"
echo "║   NightHunt Dedicated Server             ║"
echo "╚══════════════════════════════════════════╝"
echo ""
echo "  Server ID   : ${SERVER_ID:-NOT_SET}"
echo "  Game Port   : ${GAME_PORT:-7777}"
echo "  Backend URL : ${BACKEND_URL:-NOT_SET}"
echo "  Max Players : ${MAX_PLAYERS:-16}"
echo ""

# ── Validate required environment variables ───────────────────────────────────
MISSING_VARS=0

if [ -z "${SERVER_ID}" ]; then
    echo "❌ ERROR: SERVER_ID env var is required"
    MISSING_VARS=1
fi

if [ -z "${SERVER_SECRET}" ]; then
    echo "❌ ERROR: SERVER_SECRET env var is required"
    MISSING_VARS=1
fi

if [ -z "${BACKEND_URL}" ]; then
    echo "❌ ERROR: BACKEND_URL env var is required"
    MISSING_VARS=1
fi

if [ "$MISSING_VARS" -eq 1 ]; then
    echo ""
    echo "Container will exit. Backend must pass all required ENV vars."
    exit 1
fi

echo "✅ Environment validated"
echo "🚀 Starting Unity Dedicated Server..."
echo ""

# ── Start Unity DS ────────────────────────────────────────────────────────────
# Log file: /app/logs/server-<timestamp>.log
exec /app/NightHuntServer                              \
    -batchmode                                         \
    -nographics                                        \
    -logFile /app/logs/server-$(date +%Y%m%d-%H%M%S).log \
    --serverId     "${SERVER_ID}"                      \
    --serverPort   "${GAME_PORT:-7777}"                \
    --backendUrl   "${BACKEND_URL}"                    \
    --serverSecret "${SERVER_SECRET}"                  \
    --maxPlayers   "${MAX_PLAYERS:-16}"
