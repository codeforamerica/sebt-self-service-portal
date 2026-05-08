#!/usr/bin/env python3
"""
Reference implementation of the SEBT analytics App ID hash.

Produces the same digest as the portal backend's
`IIdentifierHasher.HashForAnalytics(...)` so downstream consumers (CO program
staff, OIT pipelines) can join Mixpanel / Amplitude exports back to state
program data on a shared deterministic hash.

Spec: docs/analytics/hashed-sebt-app-id.md

Usage:
    python3 hash_sebt_app_id.py <sebt_app_id> <secret>

Example (the documented test vector):
    python3 hash_sebt_app_id.py 'APP-2024-0001' 'TestVectorSecret_AtLeast32Bytes_!!!!'
    -> ca383d90647e371547d6e66297cda8089b81fc1c5cb30da6cfcbdf744d9e2861
"""

import hmac
import hashlib
import sys


def hash_sebt_app_id(sebt_app_id: str, secret: str) -> str:
    """
    Returns the lowercase-hex HMAC-SHA256 of the SEBT App ID.

    The input is used as-is (no trimming, no casing changes, no dash stripping)
    so the digest is reproducible by any consumer holding the raw App ID.
    """
    if sebt_app_id is None or sebt_app_id.strip() == "":
        raise ValueError("sebt_app_id must be a non-empty, non-whitespace string")
    if secret is None or len(secret.encode("utf-8")) < 32:
        raise ValueError("secret must be at least 32 bytes UTF-8")

    return hmac.new(
        secret.encode("utf-8"),
        sebt_app_id.encode("utf-8"),
        hashlib.sha256,
    ).hexdigest()


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2

    _, sebt_app_id, secret = argv
    print(hash_sebt_app_id(sebt_app_id, secret))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
