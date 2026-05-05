# 15. PII encryption at rest (AES-256-GCM + key rotation)

Date: 2026-05-04

## Status

Accepted

## Context

The portal persists personally identifiable information (PII) that must be shown again to the user or used in flows after it is stored (for example, email, phone, program identifiers, and date of birth from ID proofing). One-way hashing is appropriate for matching-only fields (for example, SSN via `IIdentifierHasher`), but reversible protection is required for fields that are read back in clear form at the application layer.

We also need email lookup by equality in SQL without storing a searchable plaintext email. A deterministic lookup fingerprint (HMAC over the normalized address) supports indexed lookup while ciphertext remains non-deterministic under AES-GCM.

## Decision

1. **Algorithm and envelope.** Use AES-256-GCM with a random nonce per encryption, prefixed by a stable ASCII sentinel (`sep-pii:v1:`). The ciphertext column stores UTF-8 text that embeds **key id**, nonce, tag, and payload so decryption can resolve the correct key from a configured ring.

2. **Key management.** Configure `PiiEncryption:ActiveKeyId` and `PiiEncryption:Keys[]` (`KeyId` + Base64-encoded 256-bit material). Implementations resolve keys via `PiiEncryptionSettings.ResolveKeyRing()`. New columns or environments add keys without schema changes.

3. **Rotation vs. backfill.** **Key rotation** (moving ciphertext from an older key id to the current `ActiveKeyId`) is done with `IPiiSymmetricEncryption.ReSealWithActiveEncryptor(...)` in an operational or batch job — decrypt using the key id embedded in the envelope, then encrypt with the active key. **Startup backfill** (`PiiPlaintextEncryptionBackfill`) is separate: it runs after migrations to encrypt legacy plaintext at rest and to attach `EmailHash` where the email column is already an envelope but `EmailHash` was missing — it does not, by itself, re-wrap every row when only `ActiveKeyId` changes.

4. **Email lookup.** Persist `Email` as ciphertext and `EmailHash` as `IEmailLookupHasher.HashNormalized(...)`. Queries use equality on `EmailHash`, with transitional fallbacks where legacy plaintext rows remain. The unique index applies to non-null `EmailHash` values. Bulk adds via `IDataSeeder.AddUsers` / `AddUsersAsync` skip users whose normalized email is already recognized by `GetExistingUserEmails*` / `GetExistingUserEmailsAsync`, so a legacy plaintext row cannot sit beside a duplicate insert for the same address.

5. **Profile update vs. legacy plaintext.** User updates encrypt email and populate `EmailHash`. Before saving, `DatabaseUserRepository` rejects updates when another user (different Id) still stores the target address as plaintext (EmailHash null and Email column equals the normalized address without the envelope prefix), avoiding silent duplicates alongside the filtered unique index on `EmailHash`.

6. **Failure semantics.** Authenticated decryption failures surface as `PiiDecryptException` (wrapping the underlying crypto error) so corruption or tampering is explicit rather than returning empty strings.

7. **Operational guardrails.** Logging for encryption backfill and seed paths must not include decrypted or plaintext PII; messages use counts and generic conflict text only.

## Consequences

- **Pros:** Strong confidentiality at rest for reversible PII, forward-compatible key rotation, indexed email lookup without plaintext email in the database, and a single encryption path for future columns that need the same pattern.
- **Cons:** Key material must be managed like other secrets (rotation runbook, secure distribution). Decryption is required on every read path touched by repositories; performance impact is small relative to I/O but must be kept in hot paths. Production startup rejects placeholder `PiiEncryption` keys (`PiiEncryptionGuard`), mirroring `IdentifierHasherGuard`.
- **Migration:** SQL Server `date`-typed `Users.DateOfBirth` is converted to `nvarchar` via a migration batch that uses dynamic SQL so column renames and copies parse correctly. Down-migration is intentionally unsupported once ciphertext is written.
- **Email lookup keying:** `EmailLookupHasher` derives its HMAC key from the same `IdentifierHasher:SecretKey` used for other deterministic hashes, with a distinct domain prefix so message formats do not collide. Rotating that secret affects **both** identifier hashing and email lookup hashes — plan coordinated rotation or introduce a dedicated secret later if isolation is required.
- **Backfill failures:** If `PiiPlaintextEncryptionBackfill` throws during startup, the failure is logged at **error** severity (structured logs / Datadog). The API process still starts so transient DB issues do not brick the service; **operations should alert on that log** and re-run or fix the underlying issue until backfill completes, since plaintext-at-rest rows may remain until then.

## References

- `SEBT.Portal.Core.Services.IPiiSymmetricEncryption`, `IEmailLookupHasher`
- `SEBT.Portal.Infrastructure.Services.PiiAesGcmSymmetricEncryption`, `PiiPlaintextEncryptionBackfill`
- `SEBT.Portal.Infrastructure.Repositories.DatabaseUserRepository`, `DatabaseDocVerificationChallengeRepository`
- Unit tests: `PiiAesGcmSymmetricEncryptionTests`, `PiiPlaintextEncryptionBackfillTests`, `PiiEncryptionGuardTests`, `PiiEncryptionSettingsValidatorTests`
- Production guard: `SEBT.Portal.Api.Startup.PiiEncryptionGuard`; options coherence: `SEBT.Portal.Infrastructure.Configuration.PiiEncryptionSettingsValidator`
