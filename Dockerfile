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
COPY src/SEBT.Portal.Web/package.json ./src/SEBT.Portal.Web/
COPY src/SEBT.Portal.Web/design/scripts/ ./src/SEBT.Portal.Web/design/scripts/

# Install dependencies (cached unless package files change)
RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm install --frozen-lockfile

# Copy remaining frontend source files
COPY src/SEBT.Portal.Web/ ./src/SEBT.Portal.Web/

# Build frontend with state-specific configuration
ENV STATE=${STATE}
ENV NODE_ENV=production
RUN pnpm --filter @sebt/web build

# ============================================
# Stage 2: .NET Build & Publish
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project files for restore caching
COPY src/SEBT.Portal.Api/SEBT.Portal.Api.csproj SEBT.Portal.Api/
COPY src/SEBT.Portal.Core/SEBT.Portal.Core.csproj SEBT.Portal.Core/
COPY src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj SEBT.Portal.Infrastructure/
COPY src/SEBT.Portal.Kernel/SEBT.Portal.Kernel.csproj SEBT.Portal.Kernel/
COPY src/SEBT.Portal.Kernel.AspNetCore/SEBT.Portal.Kernel.AspNetCore.csproj SEBT.Portal.Kernel.AspNetCore/
COPY src/SEBT.Portal.UseCases/SEBT.Portal.UseCases.csproj SEBT.Portal.UseCases/

RUN dotnet restore SEBT.Portal.Api/SEBT.Portal.Api.csproj

# Copy source and publish (--no-restore uses cached restore)
COPY src/SEBT.Portal.Api/ SEBT.Portal.Api/
COPY src/SEBT.Portal.Core/ SEBT.Portal.Core/
COPY src/SEBT.Portal.Infrastructure/ SEBT.Portal.Infrastructure/
COPY src/SEBT.Portal.Kernel/ SEBT.Portal.Kernel/
COPY src/SEBT.Portal.Kernel.AspNetCore/ SEBT.Portal.Kernel.AspNetCore/
COPY src/SEBT.Portal.UseCases/ SEBT.Portal.UseCases/

RUN dotnet publish SEBT.Portal.Api/SEBT.Portal.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:BuildFrontend=false

# ============================================
# Stage 3: Final Runtime Image
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy .NET application
COPY --chown=$APP_UID:$APP_UID --from=dotnet-build /app/publish .

# Copy Next.js standalone output
COPY --chown=$APP_UID:$APP_UID --from=frontend-build /app/src/SEBT.Portal.Web/.next/standalone ./frontend/
COPY --chown=$APP_UID:$APP_UID --from=frontend-build /app/src/SEBT.Portal.Web/.next/static ./frontend/.next/static/
COPY --chown=$APP_UID:$APP_UID --from=frontend-build /app/src/SEBT.Portal.Web/public ./frontend/public/

ENTRYPOINT ["dotnet", "SEBT.Portal.Api.dll"]
