# ============================================
# Stage 1: Frontend Build (cached independently)
# ============================================
FROM node:24-slim AS frontend-build
ARG STATE=dc
ARG PNPM_VERSION=10

ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"

# Enable corepack and install pnpm
RUN corepack enable && corepack prepare pnpm@${PNPM_VERSION} --activate

WORKDIR /app

# Copy package files and design scripts needed first for dependency caching
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
# Shared workspace packages the web app depends on (workspace:* + postinstall scripts)
COPY packages/design-system/ ./packages/design-system/
COPY packages/analytics/ ./packages/analytics/
COPY apps/portal/src/SEBT.Portal.Web/package.json ./apps/portal/src/SEBT.Portal.Web/
COPY apps/portal/src/SEBT.Portal.Web/design/scripts/ ./apps/portal/src/SEBT.Portal.Web/design/scripts/

# Install dependencies (cached unless package files change)
RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm install --frozen-lockfile

# Copy remaining frontend source files
COPY apps/portal/src/SEBT.Portal.Web/ ./apps/portal/src/SEBT.Portal.Web/

# Build frontend with state-specific configuration
ENV STATE=${STATE}
ENV NODE_ENV=production
ENV BUILD_STANDALONE=true
RUN pnpm --filter @sebt/web build

# ============================================
# Stage 2: .NET Build & Publish
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy build config + project files for restore caching. The repo-relative
# layout keeps the API's in-repo contract probe (apps/connectors/state) valid,
# so the plugin contract builds from source like any other monorepo build.
COPY Directory.Build.props nuget.config ./
COPY apps/connectors/Directory.Build.props apps/connectors/
COPY apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/
COPY apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj apps/portal/src/SEBT.Portal.Api/
COPY apps/portal/src/SEBT.Portal.Core/SEBT.Portal.Core.csproj apps/portal/src/SEBT.Portal.Core/
COPY apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj apps/portal/src/SEBT.Portal.Infrastructure/
COPY apps/portal/src/SEBT.Portal.Infrastructure.Seeding/SEBT.Portal.Infrastructure.Seeding.csproj apps/portal/src/SEBT.Portal.Infrastructure.Seeding/
COPY apps/portal/src/SEBT.Portal.Kernel/SEBT.Portal.Kernel.csproj apps/portal/src/SEBT.Portal.Kernel/
COPY apps/portal/src/SEBT.Portal.Kernel.AspNetCore/SEBT.Portal.Kernel.AspNetCore.csproj apps/portal/src/SEBT.Portal.Kernel.AspNetCore/
COPY apps/portal/src/SEBT.Portal.UseCases/SEBT.Portal.UseCases.csproj apps/portal/src/SEBT.Portal.UseCases/
COPY apps/portal/src/SEBT.Portal.TestUtilities/SEBT.Portal.TestUtilities.csproj apps/portal/src/SEBT.Portal.TestUtilities/

# nuget.config declares the local-plugins source; unused here (the contract is a
# ProjectReference) but the directory must exist for restore to evaluate it.
RUN mkdir -p /root/nuget-store \
  && dotnet restore apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj

# Copy source and publish (--no-restore uses cached restore)
COPY apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/ apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/
COPY apps/portal/src/SEBT.Portal.Api/ apps/portal/src/SEBT.Portal.Api/
COPY apps/portal/src/SEBT.Portal.Core/ apps/portal/src/SEBT.Portal.Core/
COPY apps/portal/src/SEBT.Portal.Infrastructure/ apps/portal/src/SEBT.Portal.Infrastructure/
COPY apps/portal/src/SEBT.Portal.Infrastructure.Seeding/ apps/portal/src/SEBT.Portal.Infrastructure.Seeding/
COPY apps/portal/src/SEBT.Portal.Kernel/ apps/portal/src/SEBT.Portal.Kernel/
COPY apps/portal/src/SEBT.Portal.Kernel.AspNetCore/ apps/portal/src/SEBT.Portal.Kernel.AspNetCore/
COPY apps/portal/src/SEBT.Portal.UseCases/ apps/portal/src/SEBT.Portal.UseCases/
COPY apps/portal/src/SEBT.Portal.TestUtilities/ apps/portal/src/SEBT.Portal.TestUtilities/

RUN dotnet publish apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:BuildFrontend=false

# ============================================
# Stage 3: Final Runtime Image
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG STATE=dc
ENV STATE=${STATE}
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy .NET application
COPY --chown=$APP_UID:$APP_UID --from=dotnet-build /app/publish .

# Copy Next.js standalone output
COPY --chown=$APP_UID:$APP_UID --from=frontend-build /app/apps/portal/src/SEBT.Portal.Web/.next/standalone ./frontend/
COPY --chown=$APP_UID:$APP_UID --from=frontend-build /app/apps/portal/src/SEBT.Portal.Web/.next/static ./frontend/.next/static/
COPY --chown=$APP_UID:$APP_UID --from=frontend-build /app/apps/portal/src/SEBT.Portal.Web/public ./frontend/public/

ENTRYPOINT ["dotnet", "SEBT.Portal.Api.dll"]
