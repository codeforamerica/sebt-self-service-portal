# Next.js standalone container image
#
# This builds the web app for a specific state (STATE build arg)
# and runs the generated standalone server.

FROM node:24-alpine AS build

WORKDIR /workspace

# Build arg controls which state is embedded at build-time.
ARG STATE=dc
ARG NEXT_PUBLIC_SOCURE_SDK_KEY=""
ARG NEXT_PUBLIC_SOCURE_DI_SDK_KEY=""
ARG NEXT_PUBLIC_MOCK_SOCURE="false"
ARG NEXT_PUBLIC_AMPLITUDE_API_KEY=""
ARG NEXT_PUBLIC_MIXPANEL_TOKEN=""
ARG NEXT_PUBLIC_SITEIMPROVE_ID=""
ARG NEXT_PUBLIC_SMARTY_EMBEDDED_KEY=""
ARG NEXT_PUBLIC_BUILD_SHA=""
ENV STATE=$STATE
ENV BUILD_STANDALONE=true

# Install dependencies (root workspace)
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./

# Copy shared design system (required by Portal Web postinstall and build scripts)
COPY packages/design-system/ ./packages/design-system/

# Copy analytics workspace package (required by web imports of @sebt/analytics)
COPY packages/analytics/ ./packages/analytics/

# Copy web workspace
COPY apps/portal/src/SEBT.Portal.Web/ ./apps/portal/src/SEBT.Portal.Web/

# Install + build web only
RUN corepack enable \
  && corepack prepare pnpm@10 --activate \
  && pnpm install --frozen-lockfile \
  && cd apps/portal/src/SEBT.Portal.Web \
  && pnpm build

FROM node:24-alpine AS runtime

ENV NODE_ENV=production
ENV PORT=3000

WORKDIR /app

# Non-root user for security (mirrors the enrollment checker's Dockerfile.ssr)
RUN addgroup -S app && adduser -S app -G app

# Next standalone output
COPY --from=build /workspace/apps/portal/src/SEBT.Portal.Web/.next/standalone ./

# Copy static files to the correct location relative to server.js
COPY --from=build /workspace/apps/portal/src/SEBT.Portal.Web/.next/static ./apps/portal/src/SEBT.Portal.Web/.next/static
COPY --from=build /workspace/apps/portal/src/SEBT.Portal.Web/public ./apps/portal/src/SEBT.Portal.Web/public

# The only path Next writes at runtime; everything else stays root-owned read-only.
RUN mkdir -p apps/portal/src/SEBT.Portal.Web/.next/cache \
  && chown -R app:app apps/portal/src/SEBT.Portal.Web/.next/cache

USER app
EXPOSE 3000

# Use the server.js from the standalone output
WORKDIR /app/apps/portal/src/SEBT.Portal.Web
CMD ["node", "server.js"]
