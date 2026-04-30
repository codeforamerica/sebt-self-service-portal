# API-only image for production (ECS). Web is a separate Next.js service.
# For local dev with API+frontend combined, use the root Dockerfile.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project files for restore caching
COPY src/SEBT.Portal.Api/SEBT.Portal.Api.csproj SEBT.Portal.Api/
COPY src/SEBT.Portal.Core/SEBT.Portal.Core.csproj SEBT.Portal.Core/
COPY src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj SEBT.Portal.Infrastructure/
COPY src/SEBT.Portal.Infrastructure.Seeding/SEBT.Portal.Infrastructure.Seeding.csproj SEBT.Portal.Infrastructure.Seeding/
COPY src/SEBT.Portal.Kernel/SEBT.Portal.Kernel.csproj SEBT.Portal.Kernel/
COPY src/SEBT.Portal.Kernel.AspNetCore/SEBT.Portal.Kernel.AspNetCore.csproj SEBT.Portal.Kernel.AspNetCore/
COPY src/SEBT.Portal.UseCases/SEBT.Portal.UseCases.csproj SEBT.Portal.UseCases/
COPY src/SEBT.Portal.TestUtilities/SEBT.Portal.TestUtilities.csproj SEBT.Portal.TestUtilities/

# Copy local NuGet packages (plugin interfaces built in CI)
COPY nuget-store/ /root/nuget-store/

RUN dotnet restore SEBT.Portal.Api/SEBT.Portal.Api.csproj

# Copy source and publish
COPY src/SEBT.Portal.Api/ SEBT.Portal.Api/
COPY src/SEBT.Portal.Core/ SEBT.Portal.Core/
COPY src/SEBT.Portal.Infrastructure/ SEBT.Portal.Infrastructure/
COPY src/SEBT.Portal.Infrastructure.Seeding/ SEBT.Portal.Infrastructure.Seeding/
COPY src/SEBT.Portal.Kernel/ SEBT.Portal.Kernel/
COPY src/SEBT.Portal.Kernel.AspNetCore/ SEBT.Portal.Kernel.AspNetCore/
COPY src/SEBT.Portal.UseCases/ SEBT.Portal.UseCases/
COPY src/SEBT.Portal.TestUtilities/ SEBT.Portal.TestUtilities/

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

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SEBT.Portal.Api.dll"]
