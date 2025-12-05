FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

# Install Node.js and pnpm in the build stage
RUN apt-get update && apt-get install -y curl
RUN curl -fsSL https://deb.nodesource.com/setup_24.x | bash -
RUN apt-get install -y nodejs
RUN corepack enable
RUN corepack prepare pnpm@latest --activate

# Install dependencies
WORKDIR /src
# Copy package files first
COPY ["package.json", "./"]
COPY ["pnpm-lock.yaml", "./"]
COPY ["pnpm-workspace.yaml", "./"]

COPY ["src/SEBT.Portal.Api/SEBT.Portal.Api.csproj", "SEBT.Portal.Api/"]
COPY ["src/SEBT.Portal.Core/SEBT.Portal.Core.csproj", "SEBT.Portal.Core/"]
COPY ["src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj", "SEBT.Portal.Infrastructure/"]
COPY ["src/SEBT.Portal.Kernel/SEBT.Portal.Kernel.csproj", "SEBT.Portal.Kernel/"]
COPY ["src/SEBT.Portal.Kernel.AspNetCore/SEBT.Portal.Kernel.AspNetCore.csproj", "SEBT.Portal.Kernel.AspNetCore/"]
COPY ["src/SEBT.Portal.UseCases/SEBT.Portal.UseCases.csproj", "SEBT.Portal.UseCases/"]

RUN dotnet restore "SEBT.Portal.Api/SEBT.Portal.Api.csproj"

# Copy all source files BEFORE building
COPY ["src/SEBT.Portal.Web/", "src/SEBT.Portal.Web/"]
COPY ["src/SEBT.Portal.Api/", "SEBT.Portal.Api/"]
COPY ["src/SEBT.Portal.Core/", "SEBT.Portal.Core/"]
COPY ["src/SEBT.Portal.Infrastructure/", "SEBT.Portal.Infrastructure/"]
COPY ["src/SEBT.Portal.Kernel/", "SEBT.Portal.Kernel/"]
COPY ["src/SEBT.Portal.Kernel.AspNetCore/", "SEBT.Portal.Kernel.AspNetCore/"]
COPY ["src/SEBT.Portal.UseCases/", "SEBT.Portal.UseCases/"]

RUN dotnet build "SEBT.Portal.Api/SEBT.Portal.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SEBT.Portal.Api/SEBT.Portal.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SEBT.Portal.Api.dll"]
