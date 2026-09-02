# Next.js standalone container image
#
# The image is state-agnostic: every state's theme stylesheet, fonts, and locale
# bundles are built in, and STATE is read from the container environment at
# request time. One image can therefore run as DC or CO.
# and runs the generated standalone server.

FROM node:24-alpine AS build

WORKDIR /workspace

# Build arg controls which state is embedded at build-time.
# Browser-facing config (analytics, Socure, Smarty) is NOT a build input: it is
# read from the container environment per request and served to the client, so
# one image runs in any environment. Set those variables on the running
# container, not here. Only build-identity values stay baked in.
ARG NEXT_PUBLIC_BUILD_SHA=""
# DC only — the dc-connector repo's resolved commit SHA at build time. Empty for CO,
# whose connector lives in-repo (nothing separate to track).
ARG NEXT_PUBLIC_DC_CONNECTOR_SHA=""
ENV BUILD_STANDALONE=true

# Install dependencies (root workspace)
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./

# Copy shared design system (required by Portal Web postinstall and build scripts)
COPY packages/design-system/ ./packages/design-system/

# Copy analytics workspace package (required by web imports of @sebt/analytics)
COPY packages/analytics/ ./packages/analytics/

# Copy observability workspace package (required by web imports of @sebt/observability)
COPY packages/observability/ ./packages/observability/

# Copy web workspace
COPY apps/portal/src/SEBT.Portal.Web/ ./apps/portal/src/SEBT.Portal.Web/

# Install + build web only.
# Drop the base image's npm after pnpm is active — npm ships a vulnerable
# node-tar (CVE-2026-59873 / CVE-2026-59874) that we never invoke.
RUN corepack enable \
  && corepack prepare pnpm@10 --activate \
  && rm -rf /usr/local/lib/node_modules/npm /usr/local/bin/npm /usr/local/bin/npx \
  && pnpm install --frozen-lockfile \
  && cd apps/portal/src/SEBT.Portal.Web \
  && pnpm build

FROM node:24-alpine AS runtime

ENV NODE_ENV=production
ENV PORT=3000

WORKDIR /app

# Runtime only needs the node binary. Remove npm/npx (and nested node-tar)
# so container scans don't flag CVE-2026-59873 / CVE-2026-59874 from the base image.
RUN rm -rf /usr/local/lib/node_modules/npm /usr/local/bin/npm /usr/local/bin/npx \
  && addgroup -S app && adduser -S app -G app

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
