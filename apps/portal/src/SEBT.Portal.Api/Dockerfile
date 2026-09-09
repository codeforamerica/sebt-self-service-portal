# API-only image for production (ECS). Web is a separate Next.js service.
# For local dev with API+frontend combined, use the root Dockerfile.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
# deploy-ecr.yaml passes --build-arg CI=true; RestoreLockedMode in Directory.Build.props
# keys off $(CI), so the image restores the exact committed lockfile versions or fails.
ARG CI

WORKDIR /src

# Project files + committed lockfiles first, for restore-layer caching. Projects are
# copied to their real repo-relative paths (not flattened) so Api.csproj's relative
# reference to the state-connector contract resolves and the contract builds from
# source as a ProjectReference — matching the committed (ProjectReference-mode)
# lockfile so locked-mode restore validates instead of silently rewriting.
COPY apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj apps/portal/src/SEBT.Portal.Api/packages.lock.json apps/portal/src/SEBT.Portal.Api/
COPY apps/portal/src/SEBT.Portal.Core/SEBT.Portal.Core.csproj apps/portal/src/SEBT.Portal.Core/packages.lock.json apps/portal/src/SEBT.Portal.Core/
COPY apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj apps/portal/src/SEBT.Portal.Infrastructure/packages.lock.json apps/portal/src/SEBT.Portal.Infrastructure/
COPY apps/portal/src/SEBT.Portal.Infrastructure.Seeding/SEBT.Portal.Infrastructure.Seeding.csproj apps/portal/src/SEBT.Portal.Infrastructure.Seeding/packages.lock.json apps/portal/src/SEBT.Portal.Infrastructure.Seeding/
COPY apps/portal/src/SEBT.Portal.Kernel/SEBT.Portal.Kernel.csproj apps/portal/src/SEBT.Portal.Kernel/packages.lock.json apps/portal/src/SEBT.Portal.Kernel/
COPY apps/portal/src/SEBT.Portal.Kernel.AspNetCore/SEBT.Portal.Kernel.AspNetCore.csproj apps/portal/src/SEBT.Portal.Kernel.AspNetCore/packages.lock.json apps/portal/src/SEBT.Portal.Kernel.AspNetCore/
COPY apps/portal/src/SEBT.Portal.UseCases/SEBT.Portal.UseCases.csproj apps/portal/src/SEBT.Portal.UseCases/packages.lock.json apps/portal/src/SEBT.Portal.UseCases/
COPY apps/portal/src/SEBT.Portal.TestUtilities/SEBT.Portal.TestUtilities.csproj apps/portal/src/SEBT.Portal.TestUtilities/packages.lock.json apps/portal/src/SEBT.Portal.TestUtilities/
COPY apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/packages.lock.json apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/

# Repo NuGet config (packageSourceMapping — dependency-confusion guard, defense in
# depth) and repo-wide MSBuild defaults (NuGet audit, lockfile + locked-mode policy).
COPY nuget.config .
COPY Directory.Build.props .

RUN dotnet restore apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj

# Copy source and publish
COPY apps/portal/src/SEBT.Portal.Api/ apps/portal/src/SEBT.Portal.Api/
COPY apps/portal/src/SEBT.Portal.Core/ apps/portal/src/SEBT.Portal.Core/
COPY apps/portal/src/SEBT.Portal.Infrastructure/ apps/portal/src/SEBT.Portal.Infrastructure/
COPY apps/portal/src/SEBT.Portal.Infrastructure.Seeding/ apps/portal/src/SEBT.Portal.Infrastructure.Seeding/
COPY apps/portal/src/SEBT.Portal.Kernel/ apps/portal/src/SEBT.Portal.Kernel/
COPY apps/portal/src/SEBT.Portal.Kernel.AspNetCore/ apps/portal/src/SEBT.Portal.Kernel.AspNetCore/
COPY apps/portal/src/SEBT.Portal.UseCases/ apps/portal/src/SEBT.Portal.UseCases/
COPY apps/portal/src/SEBT.Portal.TestUtilities/ apps/portal/src/SEBT.Portal.TestUtilities/
COPY apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/ apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/

RUN dotnet publish apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:BuildFrontend=false \
    /p:GeneratePackageOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Run as the image's built-in non-root user (mirrors the root local-dev Dockerfile).
USER $APP_UID
COPY --chown=$APP_UID:$APP_UID --from=build /app/publish .

ENTRYPOINT ["dotnet", "SEBT.Portal.Api.dll"]
