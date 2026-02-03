# Stage 1: Restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY src/LockNListen.Api/*.csproj ./src/LockNListen.Api/
COPY src/LockNListen.Domain/*.csproj ./src/LockNListen.Domain/
COPY src/LockNListen.Infrastructure/*.csproj ./src/LockNListen.Infrastructure/
RUN dotnet restore src/LockNListen.Api/LockNListen.Api.csproj

# Stage 2: Build
FROM restore AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet build src/LockNListen.Api/LockNListen.Api.csproj -c Release --no-restore

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish src/LockNListen.Api/LockNListen.Api.csproj -c Release -o /app --no-build

# Stage 4: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "LockNListen.Api.dll"]