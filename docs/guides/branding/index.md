---
description: How to change the portal's color, type, and imagery for your state, using the design token file and the per-state asset directory.
keywords: design tokens branding theme color type font logo seal favicon images assets USWDS Figma tokens.css SASS generate-tokens generate-fonts state assets
---

# Change how the portal looks

Two inputs control the portal's appearance: a design token file that sets color, type, and shape, and a small set
of image files. Both are read when the front end is built, so a change here means a rebuild rather than a restart.

## Design tokens

One file per state lives in `packages/design-system/design/states/`, named for the state. It is exported from Figma
and has two parts:

- **`system`** is the U.S. Web Design System base palette. It is the same in every state and rarely changes.
- **`theme`** is your state's layer on top: primary and secondary color families, button colors and corner shape,
  link and focus colors, font families and the roles they fill, and text measure.

Editing the `theme` half is what rebrands the portal. You do not touch stylesheets.

### What the token file generates

Four generators read it, and the `STATE` environment variable decides which state's file they read.

| Command | Produces | Used for |
| --- | --- | --- |
| `pnpm tokens` | `design/tokens.css` | CSS custom properties, imported by the app layout |
| `pnpm tokens:sass` | Three SASS files | Theming USWDS components when their stylesheet compiles |
| `pnpm tokens:fonts` | `fonts.ts` | Loading each font family the tokens name |
| `pnpm tokens:all` | All of the above, for every state | A build artifact that carries more than one state |

All four run automatically before `pnpm dev` and `pnpm build`, so you rarely call them yourself. To change a token,
edit the state's JSON and start the app.

The generated files are output. Editing `tokens.css` or the SASS files directly works until the next build
overwrites them.

> [!NOTE]
> Token generation is cached on file timestamps, and regenerates only when the JSON is newer than the file it
> produced. If you restored an older token file, or copied one in with its timestamp preserved, the generator
> decides nothing changed and skips the work. Touch the JSON to force it.

### Fonts

A font family named in the tokens has to be one the generator knows how to load. It looks in two places: a list of
Google fonts, and a set of fonts vendored into the repository. Urbanist, Atkinson Hyperlegible, and Museo Slab are
vendored today, which is how Washington, DC and Colorado each get their own typeface.

A family in neither list logs a warning and is skipped. The build still succeeds and the page still renders, in a
fallback font. If type looks wrong after a rebrand, read the generator's output before looking anywhere else.

## Site assets

Per-state images live under `public/images/states/`, in a directory named for the state:

- `logo.svg` appears in the header.
- `seal.svg` appears in the footer.
- `icons/` holds any icons specific to your state.

These are found by convention rather than by configuration. Components build the path from the current state, so a
new state needs a directory with the same filenames rather than an entry in a config file. A missing file is a
broken image at runtime, not an error at build time, so check every page after adding a state.

Favicons live in `public/img/favicons/` and are shared across states rather than set per state.

## Next

With style, assets, and content in place, move on to [Build the portal](../build/index.md).
