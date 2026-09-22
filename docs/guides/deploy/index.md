---
description: Putting a build of the Summer EBT Self-Service Portal onto servers you run, and confirming it worked.
keywords: deploy deployment production containers ECS IIS Windows Server hosting bundle HttpPlatformHandler configure server application STATE overlay secrets ports health check rollback update
---

# Deploy the portal

This page covers putting a build onto servers you run. For getting the stack running on your own machine instead,
see [Set up your development environment](../local-setup/index.md).

There are two ways to deploy it, and both are in production today. Colorado runs Linux containers on Amazon ECS.
Washington, DC runs Windows Server with IIS. The application code is the same in both; what differs is how it is
packaged and what the server needs installed.

## The order of the steps

1. **Prepare the database.** The portal defines its own schema, and there are three ways to create it depending on
   who applies schema changes in your organization. See [Prepare the database](database.md).
2. **Configure the server.** The host itself: the runtimes and modules listed below, the account the application
   runs under, write permission on the log directories, TLS, and which ports are reachable. This is done once per
   server and survives releases.
3. **Configure the application.** The `STATE` environment variable selects your state's settings, which carry the
   identity verification rules, the sign-in settings, and the feature flags. Connection strings and secrets come
   from environment variables or secrets files, never from a file baked into the build.
4. **Run it and confirm.** Start the API and at least one front end, then work through the checks below.

Anything that has to be decided or built first is covered in [Plan your program](../plan/index.md) and
[Build the portal](../build/index.md). In particular, the keys that run in the browser cannot be fixed at this
stage. If one was missing when the build ran, you need a new build.

## What the server needs

Linux containers need a container runtime such as Docker or Podman and nothing else. Both images run as a non-root user. The API listens on
ports 8080 and 8081, and the web tier on port 3000.

Windows Server with IIS needs more installed up front, once per server:

- Windows Server 2019 or later, with IIS enabled.
- The ASP.NET Core 10 Hosting Bundle, for the API. Run `iisreset` once after installing it.
- Node.js 24 LTS, x64, for the web tier.
- The HttpPlatformHandler module for IIS, which is what runs the Next.js server behind IIS.

None of these are bundled in a release. Install them before the first deployment.

## Confirm the deployment

Check three things, in this order:

1. The API log says `Database migrations completed successfully`. A failed migration does not stop the application,
   so this line is the only reliable signal. See [Confirm it worked](database.md#confirm-it-worked).
2. `/api/health` answers. Note that its database check only proves connectivity, not that the schema exists.
3. The front end loads and a test account can sign in.

## Updating to a new version

Stage the new version in a separate directory or task definition rather than overwriting the running one. Apply any
database change first, and only move traffic once it is complete. Keep the previous version in place until the
release is accepted, so a rollback does not need a rebuild.

Releases and what changed in each are listed on
[GitHub Releases](https://github.com/codeforamerica/sebt-self-service-portal/releases).
