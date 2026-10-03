// USWDS Asset Copy Script
// Copies static assets from USWDS node_modules to the public directory.
// Runs during postinstall so the assets are available for Next.js.
//
// This is Node and not a shell script because postinstall runs on every
// developer machine, and Windows has no `sh`. Node is already a prerequisite of
// this repository, so it is the one interpreter every machine here has.
//
// The output stays ASCII for the same reason: the Windows console renders the
// default code page, and it turns box-drawing and arrows into mojibake.

import { cpSync, existsSync, mkdirSync, readdirSync } from "node:fs";
import { join } from "node:path";

const USWDS_DIST = join("node_modules", "@uswds", "uswds", "dist");
const PUBLIC_DIR = "public";

// Sentinel files must be USWDS-specific. public/fonts is non-empty in git
// (museo-slab), so its presence proves nothing.
const sentinels = [
  join(PUBLIC_DIR, "js", "uswds-init.min.js"),
  join(PUBLIC_DIR, "img", "sprite.svg"),
];

if (sentinels.every((file) => existsSync(file))) {
  console.log("USWDS assets already exist, skipping copy");
  process.exit(0);
}

if (!existsSync(USWDS_DIST)) {
  console.error(
    `Could not find ${USWDS_DIST}. Run this from a package that depends on @uswds/uswds, after its dependencies are installed.`,
  );
  process.exit(1);
}

console.log("Copying USWDS assets to public directory...");

for (const dir of ["js", "css", "fonts", "img"]) {
  mkdirSync(join(PUBLIC_DIR, dir), { recursive: true });
}

// dereference turns any symlink in the package into a real file. A static
// directory that Next.js serves wants files, and pnpm's store is full of links.
const copy = (from, to) =>
  cpSync(from, to, { recursive: true, dereference: true });

console.log("  Copying JavaScript...");
copy(
  join(USWDS_DIST, "js", "uswds-init.min.js"),
  join(PUBLIC_DIR, "js", "uswds-init.min.js"),
);

console.log("  Copying CSS...");
copy(
  join(USWDS_DIST, "css", "uswds.min.css"),
  join(PUBLIC_DIR, "css", "uswds.min.css"),
);

// The shell original copied `fonts/*`, which is the contents and not the
// directory itself, so this walks the entries to keep that shape.
console.log("  Copying fonts...");
for (const entry of readdirSync(join(USWDS_DIST, "fonts"))) {
  copy(join(USWDS_DIST, "fonts", entry), join(PUBLIC_DIR, "fonts", entry));
}

console.log("  Copying images...");
copy(join(USWDS_DIST, "img"), join(PUBLIC_DIR, "img"));

console.log("USWDS assets copied successfully");
