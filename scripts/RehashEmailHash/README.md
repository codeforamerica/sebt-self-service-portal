# Rehash `Users.EmailHash`

Manual ops tool to rewrite `Users.EmailHash` under a chosen `IdentifierHasher:SecretKey`
by decrypting stored `Email` (AES-GCM envelope or legacy plaintext).

Use when rotating the identifier/email lookup secret, or to heal rows hashed under a
different secret than the running API.

This does **not** rehash other `IdentifierHasher` columns (SSN, card-replacement
cooldown hashes, and similar).

## Prerequisites

Configure (appsettings under `apps/portal/src/SEBT.Portal.Api` and/or env vars):

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Target database |
| `PiiEncryption:*` | Decrypt `Email` envelopes (same keys the API uses) |
| `IdentifierHasher:SecretKey` | **Target** secret: hashes are rewritten under this value |

Set `IdentifierHasher:SecretKey` to the secret you want parity with **before** running
(typically the secret the API will use after cutover).

## Usage

From the repo root:

```bash
# Report what would change (no writes)
dotnet run --project scripts/RehashEmailHash -- --dry-run

# Apply (prompts for "yes")
dotnet run --project scripts/RehashEmailHash --

# Apply without prompt (automation / bastion)
dotnet run --project scripts/RehashEmailHash -- --yes
```

Exit codes:

| Code | Meaning |
| --- | --- |
| 0 | Success (no unresolved collisions) |
| 1 | Hard failure (config, DB, unexpected error) |
| 2 | Completed but skipped collision rows (dedupe those user Ids, then re-run) |

## Collisions

If two (or more) `Users` rows decrypt to the same normalized email, they would share
one `EmailHash` and violate `IX_Users_EmailHash`. Those rows are skipped and their
Ids are printed.

That includes the case where one row already has the target hash and another is a
duplicate of the same address: both Ids appear in the collision list, the
already-matching row is left alone, and the duplicate is not rewritten. Merge or
delete the duplicate Id, then re-run.

## Suggested cutover

1. `--dry-run` against a DC copy; review counts and collisions.
2. Dedupe any collision Ids.
3. Run with `--yes` while the API still uses the **old** secret **or** during a
   maintenance window. Login will miss until the API secret matches the rewritten hashes.
4. Deploy/flip `IdentifierHasher:SecretKey` to the same value used for the rehash.
5. Spot-check login for a known user.
