# =====================================================
# IVF Frontend — Multi-Stage Docker Build
# Angular 21 → Caddy 2 Alpine
# =====================================================
# Build: docker build -t ghcr.io/hung6066/ivf-client:latest -f ivf-client/Dockerfile .
# Run:   docker run -p 8080:80 ghcr.io/hung6066/ivf-client:latest
# =====================================================

# ──────────── Stage 1: Build Angular ────────────
FROM node:20-alpine AS build

WORKDIR /app

# Copy package files first (cache layer)
COPY ivf-client/package.json ivf-client/package-lock.json ./

# Install dependencies
RUN npm ci --ignore-scripts

# Copy source code
COPY ivf-client/ .

# Build production
RUN npm run build -- --configuration production

# ──────────── Stage 2: Serve with Caddy ────────────
FROM caddy:2-alpine AS runtime

# Copy built Angular app
COPY --from=build /app/dist/ivf-client/browser /srv/frontend

# Copy Caddyfile for SPA serving
COPY ivf-client/Caddyfile /etc/caddy/Caddyfile

EXPOSE 80

# Caddy serves on port 80
CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
