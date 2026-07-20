# Public Assets

This directory contains static assets served by Next.js.

## USWDS Assets

After running `pnpm install`, you need to copy USWDS assets here:

```bash
# Copy USWDS JavaScript
cp node_modules/@uswds/uswds/dist/js/uswds-init.min.js public/js/

# Copy USWDS sprite
cp node_modules/@uswds/uswds/dist/img/sprite.svg public/img/
```

Or use the provided script:

```bash
pnpm copy-uswds-assets
```

## Directory Structure

```
public/
├── js/
│   └── uswds-init.min.js (USWDS initialization script)
├── img/
│   └── sprite.svg (USWDS icon sprite)
└── favicon.ico (optional)
```
