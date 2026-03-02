#!/bin/bash
# =============================================================================
# NightHunt DS - Docker Health Check
# Docker HEALTHCHECK CMD: /app/health-check.sh
#
# Strategy: Kiểm tra file /app/logs/.healthy được tạo bởi ServerBootstrap
#           sau khi đăng ký thành công với backend.
# =============================================================================

HEALTH_FILE="/app/logs/.healthy"
MAX_AGE_SECONDS=120  # File phải được cập nhật trong vòng 120s

if [ ! -f "$HEALTH_FILE" ]; then
    # Server chưa ready (vẫn đang boot) - trả về 1 để Docker retry
    exit 1
fi

# Kiểm tra thời gian cập nhật file (server phải còn alive)
FILE_AGE=$(( $(date +%s) - $(stat -c %Y "$HEALTH_FILE" 2>/dev/null || echo 0) ))

if [ "$FILE_AGE" -gt "$MAX_AGE_SECONDS" ]; then
    # File quá cũ → server có thể đã bị treo
    exit 1
fi

exit 0
