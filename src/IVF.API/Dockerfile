# =====================================================
# IVF Backend — Multi-Stage Docker Build
# .NET 10 — optimized for cache & image size
# =====================================================

# ──────────── Stage 1: Restore (cached unless *.csproj changes) ────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS restore
WORKDIR /src

# Copy only project files + Directory.Build.props for restore cache
COPY Directory.Build.props .
COPY src/IVF.API/IVF.API.csproj              src/IVF.API/
COPY src/IVF.Application/IVF.Application.csproj  src/IVF.Application/
COPY src/IVF.Domain/IVF.Domain.csproj            src/IVF.Domain/
COPY src/IVF.Infrastructure/IVF.Infrastructure.csproj src/IVF.Infrastructure/

# Restore with NuGet cache mount — survives across builds
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/IVF.API/IVF.API.csproj --runtime linux-musl-x64

# ──────────── Stage 2: Publish (single step, no separate build) ────────────
FROM restore AS publish
COPY src/ src/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/IVF.API/IVF.API.csproj \
    -c Release \
    -o /app/publish \
    --runtime linux-musl-x64 \
    --self-contained false \
    /p:UseAppHost=false \
    --no-restore

# ──────────── Stage 3: Runtime (Alpine — ~5x smaller than Debian) ────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Install Docker CLI (static binary — no apt, no daemon) for Swarm management
RUN apk add --no-cache docker-cli curl

COPY --from=publish /app/publish .
RUN mkdir -p /app/certs

EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "IVF.API.dll"]
