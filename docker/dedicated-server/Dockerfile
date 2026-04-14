# ── Stage 1: Base Ubuntu với runtime dependencies ─────────────────────────────
FROM ubuntu:22.04 AS base

ENV DEBIAN_FRONTEND=noninteractive

# Unity Linux server runtime dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
        libglib2.0-0      \
        libssl3            \
        libc6              \
        libstdc++6         \
        libgcc-s1          \
        curl               \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# ── Stage 2: Game Server Image ─────────────────────────────────────────────────
FROM base AS gameserver

# Non-root user (security best practice)
RUN groupadd -g 1000 gameserver \
    && useradd -u 1000 -g gameserver -m -s /bin/bash gameserver

WORKDIR /app

# Copy Unity Dedicated Server build output.
# CI/CD (GitHub Actions) sẽ copy build vào thư mục này trước khi `docker build`.
# Xem: .github/workflows/build-dedicated-server.yml → step "Prepare Docker context"
COPY --chown=gameserver:gameserver ./build/ /app/

# Copy operational scripts
COPY --chown=gameserver:gameserver ./scripts/entrypoint.sh   /app/entrypoint.sh
COPY --chown=gameserver:gameserver ./scripts/health-check.sh /app/health-check.sh

# Cấp quyền execute
RUN chmod +x /app/NightHuntDS.x86_64 \
    && chmod +x /app/entrypoint.sh \
    && chmod +x /app/health-check.sh \
    && mkdir -p /app/logs

USER gameserver

# UDP: FishNet Tugboat game traffic
EXPOSE 7777/udp

# TCP: Internal health-check endpoint (KHÔNG expose ra ngoài VPS)
EXPOSE 9090/tcp

HEALTHCHECK \
    --interval=15s  \
    --timeout=5s    \
    --start-period=45s \
    --retries=3     \
    CMD /app/health-check.sh

ENTRYPOINT ["/app/entrypoint.sh"]
