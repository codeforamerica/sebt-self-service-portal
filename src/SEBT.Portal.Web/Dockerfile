# Next.js standalone container image
#
# This builds the web app for a specific state (STATE build arg)
# and runs the generated standalone server.

FROM node:24-alpine AS build

WORKDIR /workspace

# Build arg controls which state is embedded at build-time.
ARG STATE=dc
ENV STATE=$STATE
ENV BUILD_STANDALONE=true

# Install dependencies (root workspace)
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./

# Copy shared design system (required by Portal Web postinstall and build scripts)
COPY packages/design-system/ ./packages/design-system/

# Copy web workspace
COPY src/SEBT.Portal.Web/ ./src/SEBT.Portal.Web/

# Install + build web only
RUN corepack enable \
  && corepack prepare pnpm@10 --activate \
  && pnpm install --frozen-lockfile \
  && cd src/SEBT.Portal.Web \
  && pnpm build

FROM node:24-alpine AS runtime

ENV NODE_ENV=production
ENV PORT=3000

WORKDIR /app

# Next standalone output
COPY --from=build /workspace/src/SEBT.Portal.Web/.next/standalone ./

# Copy static files to the correct location relative to server.js
COPY --from=build /workspace/src/SEBT.Portal.Web/.next/static ./src/SEBT.Portal.Web/.next/static
COPY --from=build /workspace/src/SEBT.Portal.Web/public ./src/SEBT.Portal.Web/public

EXPOSE 3000

# Use the server.js from the standalone output
WORKDIR /app/src/SEBT.Portal.Web
CMD ["node", "server.js"]
