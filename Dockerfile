# Klarrow public-demo container: API + pre-built React UI served from the same origin.
#
# Stage 1 builds the .NET API. Stage 2 builds the React UI (same-origin config:
# VITE_API_BASE empty). Stage 3 ships the ASP.NET runtime with the UI copied into wwwroot.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY ["src/RecallGraph.Api/RecallGraph.Api.csproj", "src/RecallGraph.Api/"]
COPY ["src/RecallGraph.Application/RecallGraph.Application.csproj", "src/RecallGraph.Application/"]
COPY ["src/RecallGraph.Infrastructure/RecallGraph.Infrastructure.csproj", "src/RecallGraph.Infrastructure/"]
RUN dotnet restore src/RecallGraph.Api/RecallGraph.Api.csproj
COPY ["src/", "src/"]
RUN dotnet publish src/RecallGraph.Api/RecallGraph.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM node:22-alpine AS ui-build
WORKDIR /ui
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ ./
ENV VITE_PUBLIC_DEMO=1
RUN npm run build

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
WORKDIR /app
COPY --from=api-build /app/publish/ ./
COPY --from=ui-build /ui/dist/ ./wwwroot/
# Storage root is bind-mounted (persistent disk in deployment) so SQLite + evidence survive restarts.
VOLUME ["/app/data"]
ENTRYPOINT ["sh", "-c", "exec dotnet RecallGraph.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
