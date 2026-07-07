# API-only image for production (ECS). Web is a separate Next.js service.
# For local dev with API+frontend combined, use the root Dockerfile.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project files for restore caching
COPY apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj SEBT.Portal.Api/
COPY apps/portal/src/SEBT.Portal.Core/SEBT.Portal.Core.csproj SEBT.Portal.Core/
COPY apps/portal/src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj SEBT.Portal.Infrastructure/
COPY apps/portal/src/SEBT.Portal.Infrastructure.Seeding/SEBT.Portal.Infrastructure.Seeding.csproj SEBT.Portal.Infrastructure.Seeding/
COPY apps/portal/src/SEBT.Portal.Kernel/SEBT.Portal.Kernel.csproj SEBT.Portal.Kernel/
COPY apps/portal/src/SEBT.Portal.Kernel.AspNetCore/SEBT.Portal.Kernel.AspNetCore.csproj SEBT.Portal.Kernel.AspNetCore/
COPY apps/portal/src/SEBT.Portal.UseCases/SEBT.Portal.UseCases.csproj SEBT.Portal.UseCases/
COPY apps/portal/src/SEBT.Portal.TestUtilities/SEBT.Portal.TestUtilities.csproj SEBT.Portal.TestUtilities/

# Copy local NuGet packages (plugin interfaces built in CI)
COPY nuget-store/ /root/nuget-store/

# Repo NuGet config, so packageSourceMapping (SEBT.* only from the local store —
# the dependency-confusion guard) also applies inside the image build. Its
# %HOME%/nuget-store source resolves to /root/nuget-store copied above.
COPY nuget.config .

# Repo-wide MSBuild defaults: supplies the contract fallback version
# (StateConnectorInterfacesFloatingVersion) and applies the same NuGet-audit /
# warnings-as-errors policy here as in every other build.
COPY Directory.Build.props .

RUN dotnet restore SEBT.Portal.Api/SEBT.Portal.Api.csproj

# Copy source and publish
COPY apps/portal/src/SEBT.Portal.Api/ SEBT.Portal.Api/
COPY apps/portal/src/SEBT.Portal.Core/ SEBT.Portal.Core/
COPY apps/portal/src/SEBT.Portal.Infrastructure/ SEBT.Portal.Infrastructure/
COPY apps/portal/src/SEBT.Portal.Infrastructure.Seeding/ SEBT.Portal.Infrastructure.Seeding/
COPY apps/portal/src/SEBT.Portal.Kernel/ SEBT.Portal.Kernel/
COPY apps/portal/src/SEBT.Portal.Kernel.AspNetCore/ SEBT.Portal.Kernel.AspNetCore/
COPY apps/portal/src/SEBT.Portal.UseCases/ SEBT.Portal.UseCases/
COPY apps/portal/src/SEBT.Portal.TestUtilities/ SEBT.Portal.TestUtilities/

RUN dotnet publish SEBT.Portal.Api/SEBT.Portal.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:BuildFrontend=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Run as the image's built-in non-root user (mirrors the root local-dev Dockerfile).
USER $APP_UID
COPY --chown=$APP_UID:$APP_UID --from=build /app/publish .

ENTRYPOINT ["dotnet", "SEBT.Portal.Api.dll"]
