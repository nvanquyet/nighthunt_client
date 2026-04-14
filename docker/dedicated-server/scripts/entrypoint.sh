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
echo "  Server ID        : ${SERVER_ID:-NOT_SET}"
echo "  Game Port        : ${GAME_PORT:-7777}"
echo "  Backend URL      : ${BACKEND_URL:-NOT_SET}"
echo "  Max Players      : ${MAX_PLAYERS:-16}"
echo "  Expected Players : ${EXPECTED_PLAYERS:-NOT_SET}"
echo "  Map ID           : ${MAP_ID:-map_01}"
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

# ── Find DS binary (Unity có thể output NightHuntDS hoặc NightHuntDS.x86_64) ─
if [ -f "/app/NightHuntDS.x86_64" ]; then
    DS_BINARY="/app/NightHuntDS.x86_64"
elif [ -f "/app/NightHuntDS" ]; then
    DS_BINARY="/app/NightHuntDS"
else
    echo "❌ ERROR: No DS binary found in /app/ (expected NightHuntDS or NightHuntDS.x86_64)"
    ls -la /app/
    exit 1
fi
echo "📦 Using binary: ${DS_BINARY}"

# ── Start Unity DS ────────────────────────────────────────────────────────────
# Log file: /app/logs/server-<timestamp>.log
exec "${DS_BINARY}"                                    \
    -batchmode                                         \
    -nographics                                        \
    -logFile /app/logs/server-$(date +%Y%m%d-%H%M%S).log \
    --serverId        "${SERVER_ID}"                   \
    --serverPort      "${GAME_PORT:-7777}"             \
    --backendUrl      "${BACKEND_URL}"                 \
    --serverSecret    "${SERVER_SECRET}"               \
    --maxPlayers      "${MAX_PLAYERS:-16}"             \
    --expectedPlayers "${EXPECTED_PLAYERS:-2}"         \
    --mapId           "${MAP_ID:-map_01}"
