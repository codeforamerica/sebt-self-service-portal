# Next.js standalone container image
#
# This builds the web app for a specific state (STATE build arg)
# and runs the generated standalone server.

FROM node:24-alpine AS build

WORKDIR /workspace

# Build arg controls which state is embedded at build-time.
ARG STATE=dc
ENV STATE=$STATE

# Install dependencies (root workspace)
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./

# Copy web workspace
COPY src/SEBT.Portal.Web/ ./src/SEBT.Portal.Web/

# Install + build web only
RUN corepack enable \
  && corepack prepare pnpm@10 --activate \
  && pnpm install --frozen-lockfile \
  && pnpm --filter @sebt/web build

FROM node:24-alpine AS runtime

ENV NODE_ENV=production
ENV PORT=3000

WORKDIR /app

# Next standalone output
COPY --from=build /workspace/src/SEBT.Portal.Web/.next/standalone ./

# Static assets are not bundled into standalone by default
COPY --from=build /workspace/src/SEBT.Portal.Web/.next/static ./.next/static
COPY --from=build /workspace/src/SEBT.Portal.Web/public ./public

EXPOSE 3000

CMD ["node", "server.js"]
