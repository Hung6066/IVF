# =====================================================
# IVF Frontend — Multi-Stage Docker Build
# Angular 21 → Caddy 2 Alpine
# =====================================================
# Build: docker build -t ghcr.io/hung6066/ivf-client:latest -f ivf-client/Dockerfile .
# Run:   docker run -p 8080:80 ghcr.io/hung6066/ivf-client:latest
# =====================================================

# ──────────── Stage 1: Install deps (cached unless package*.json changes) ────
FROM node:20-alpine AS deps

WORKDIR /app

# Copy package files first (cache layer)
COPY ivf-client/package.json ivf-client/package-lock.json ./

# Install dependencies with npm cache mount
RUN --mount=type=cache,target=/root/.npm \
    npm ci --ignore-scripts

# ──────────── Stage 2: Build Angular ────────────
FROM deps AS build

# Copy source code
COPY ivf-client/ .

# Build production
RUN npm run build -- --configuration production

# ──────────── Stage 3: Serve with Caddy ────────────
FROM caddy:2-alpine AS runtime

# Copy built Angular app
COPY --from=build /app/dist/ivf-client/browser /srv/frontend

# Copy Caddyfile for SPA serving
COPY ivf-client/Caddyfile /etc/caddy/Caddyfile

EXPOSE 80

# Caddy serves on port 80
CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
