---
description: The work that turns this repository into something your state can deploy, covering the connector, your style and content, and the keys the browser needs.
keywords: build portal state connector design tokens locale content spreadsheet NEXT_PUBLIC build-time environment variables container image IIS bundle packaging
---

# Build the portal

Three things turn this repository into something your state can deploy. Two of them are work you do once and then
maintain. The third is a set of values that have to be present at the moment the front end is built.

## Write your state connector

Nothing about your state ships in the box. The portal does not know where your benefit data lives until you tell it,
and you do that by writing a connector: code that reads from your systems and hands the portal back an answer in a
shape it understands.

You write it against a fixed set of methods we publish, and the portal picks it up when it starts. It can sit in
your fork of this repository, or in a repository of its own, whichever suits your team.

It mostly reads. There are only three things the portal can write back: a mailing address, a request for a
replacement card, and contact preferences. Adopting the portal does not mean handing over broad write access to your
systems.

See [Build a state connector](../state-connector/index.md) for the details.

You do not need a finished connector to deploy and test everything else. Turn on `UseMockHouseholdData` and the API
serves a handful of made-up families instead of calling your systems, which is enough to prove the runtimes, the
database, and signing in all work. Turn it off before real families use it.

## Apply your style and content

Two files carry everything a family sees, and neither of them is code.

- **Style.** Colors, type, logo, and button shape come from a design token file for your state. A rebrand is a
  change to that file, not a hunt through stylesheets.
- **Content.** Every string a family reads is generated into locale files from a spreadsheet export for your state.
  English, Spanish, and Amharic ship today, and a new language is another column rather than a code change.

The locale files themselves are generated output. Edit the spreadsheet and re-run the generator rather than editing
them by hand, or your next build will overwrite the change. See
[Change user-facing text](../content/index.md).

## Set the browser keys before you build

Some values are used by code running in the browser rather than on the server. Next.js writes those values directly
into the front end's files when it builds, which means:

- They must be present in the environment that runs the build.
- Setting them later on the server has no effect at all. The built files already contain whatever was there at build
  time, including nothing.
- When one is missing, the feature does not error. It silently does nothing, which reads like the service being
  down.

The values in this category all begin with `NEXT_PUBLIC_`, and they cover exactly the accounts opened during
[Plan your program](../plan/index.md): the Socure browser keys, the Smarty embedded key, and a key for each
analytics tool you chose. `NEXT_PUBLIC_STATE` is here too, and it is what selects your state's style and content.

If you are checking a build that already happened, the built files are the place to look. A value that was empty at
build time is empty in them, and no amount of server configuration will change it.

## What a build produces

- **For Linux containers:** two images, one for the API and one for the web tier. Both run as a non-root user.
- **For Windows Server:** one bundle holding the API files, the Next.js server, and a database package for a
  database administrator to review.

## Next

With a build in hand, move on to [Deploy the portal](../deploy/index.md).
