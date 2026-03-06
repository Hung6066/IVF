# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY ["src/IVF.API/IVF.API.csproj", "src/IVF.API/"]
COPY ["src/IVF.Application/IVF.Application.csproj", "src/IVF.Application/"]
COPY ["src/IVF.Domain/IVF.Domain.csproj", "src/IVF.Domain/"]
COPY ["src/IVF.Infrastructure/IVF.Infrastructure.csproj", "src/IVF.Infrastructure/"]

# Restore dependencies (API project only — FingerprintClient is Windows-only)
RUN dotnet restore "src/IVF.API/IVF.API.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
WORKDIR /src/src/IVF.API
RUN dotnet build -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install Docker CLI for infrastructure monitoring (Swarm management)
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates curl gnupg && \
    install -m 0755 -d /etc/apt/keyrings && \
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc && \
    chmod a+r /etc/apt/keyrings/docker.asc && \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" > /etc/apt/sources.list.d/docker.list && \
    apt-get update && apt-get install -y --no-install-recommends docker-ce-cli && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .
RUN mkdir -p /app/certs
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "IVF.API.dll"]
