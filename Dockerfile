# Multi-stage build: Angular production build -> copied into the API's wwwroot -> dotnet
# publish -> slim ASP.NET runtime image. Mirrors scripts/publish.sh, just containerized.

FROM node:24-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npx ng build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src
COPY backend/FinFlow.Core/FinFlow.Core.csproj backend/FinFlow.Core/
COPY backend/FinFlow.Api/FinFlow.Api.csproj backend/FinFlow.Api/
RUN dotnet restore backend/FinFlow.Api/FinFlow.Api.csproj
COPY backend/FinFlow.Core/ backend/FinFlow.Core/
COPY backend/FinFlow.Api/ backend/FinFlow.Api/
COPY --from=frontend-build /src/frontend/dist/finflow/browser backend/FinFlow.Api/wwwroot
RUN dotnet publish backend/FinFlow.Api/FinFlow.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish ./

# Fixed internal port and DB location — the only thing a self-hoster changes is the
# docker-compose.yml port mapping (see README's Docker section), never these.
ENV ASPNETCORE_URLS=http://+:5199
ENV ConnectionStrings__Default="Data Source=/data/finflow.db"
VOLUME /data
EXPOSE 5199

ENTRYPOINT ["dotnet", "FinFlow.Api.dll"]
