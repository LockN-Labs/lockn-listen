# LOC-45 Architecture: CI/CD Pipeline for LockN Listen

## Workflow Structure

### `.github/workflows/ci.yml`
- Triggers on PRs and push to `master`
- Steps:
  1. Checkout code
  2. Setup .NET 8 SDK
  3. Restore NuGet packages with caching
  4. Build project
  5. Run `dotnet format` check
  6. Run unit tests (when implemented)

### `.github/workflows/release.yml`
- Triggers on merge to `master`
- Steps:
  1. Checkout code
  2. Setup .NET 8 SDK
  3. Build Docker image with multi-stage build
  4. Authenticate to GHCR using `GITHUB_TOKEN`
  5. Push image to `ghcr.io/lockn-listen/api` with tags:
     - `sha-$(commit-hash)`
     - `latest`
  6. Multi-platform build: `linux/amd64`, `linux/arm64`

## Dockerfile Design

```dockerfile
# Stage 1: Restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY *.csproj .
RUN dotnet restore

# Stage 2: Build
FROM restore AS build
WORKDIR /src
COPY . .
RUN dotnet build -c Release

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app

# Stage 4: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "LockNListen.Api.dll"]
```

- Optimizations:
  - Layer caching for restore/build stages
  - Final runtime image < 200MB
  - SDK only included during build phase

## GitHub Actions Configuration

- **Caching**: `~/.nuget/packages` cached between runs
- **Matrix Strategy** (future-ready):
  ```yaml
  strategy:
    matrix:
      dotnet-version: ["8.0"]
  ```
- **GHCR Authentication**:
  ```yaml
  - name: Login to GHCR
    uses: docker/login-action@v2
    with:
      registry: ghcr.io
      username: ${{ github.actor }}
      password: ${{ github.token }}
  ```

## `docker-compose.yml`

```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - .:/app
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - LOCKNLISTEN__DATABASE=LocalDb
    stdin_open: true
    tty: true
```

## Directory Layout
```
.github/
  workflows/
    ci.yml
    release.yml
Dockerfile
docker-compose.yml
```

## Quality Gates
- ✅ CI build must succeed
- ✅ Code format compliance
- ⚠️ Unit test coverage (placeholder for future)
- 🛡️ Docker image scan (recommended addition)