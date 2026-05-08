# Hashed SEBT App ID — Analytics Spec

This document is the contract between the SEBT portal and downstream analytics consumers (CO program staff, CfA analysts, OIT). It defines exactly how a SEBT App ID is hashed for emission to vendor analytics tools so the same digest can be reproduced by external pipelines and used as a join key against state program data.

## Scope

The hashed SEBT App ID is emitted today for **Colorado** dashboards. Other states are unaffected — the API simply does not populate the field for them.

## Algorithm

```
hashed_app_id = HMAC_SHA256(secret, sebt_app_id_utf8)
```

- **Algorithm:** HMAC-SHA256
- **Key:** the configured shared secret loaded via `IdentifierHasher:SecretKey`
- **Message:** the SEBT App ID, encoded as UTF-8 with no normalization
- **Output encoding:** lowercase hexadecimal, 64 characters

### Input normalization

Use the SEBT App ID **exactly as returned** by the source system. Do not trim, lowercase, or strip dashes/spaces. Two notes:

- The portal's storage-side hasher (`IIdentifierHasher.Hash`) **does** normalize identifiers (trim, dash/space stripping) for cooldown lookups. That method must not be used for analytics — the public spec relies on raw input so external consumers can reproduce the digest from the value they hold.
- A separate method, `IIdentifierHasher.HashForAnalytics`, implements this spec and is the only path that should feed `digitalData.user.hashed_app_id`.

### Null / empty input

`HashForAnalytics` returns `null` for null, empty, or whitespace-only input. The API in turn omits the `hashedAppId` field, and the frontend does not call `setUserData('hashed_app_id', …)`. Analytics therefore sees no digest rather than a digest of an empty string.

## Test vector

A reproducible vector that every implementation (backend C#, reference Python, downstream pipelines) must match. **The secret below is for vector verification only — never use it in any environment.**

| Field | Value |
|---|---|
| Secret (32+ bytes UTF-8) | `TestVectorSecret_AtLeast32Bytes_!!!!` |
| Input (SEBT App ID) | `APP-2024-0001` |
| Expected digest | `ca383d90647e371547d6e66297cda8089b81fc1c5cb30da6cfcbdf744d9e2861` |

The backend test `IdentifierHasherTests.HashForAnalytics_TestVector_MatchesPublishedReference` asserts this exact triplet, so any change to the algorithm fails CI rather than silently desynchronizing external consumers.

## Reference implementation

A standalone reference script lives at [`scripts/hash_sebt_app_id.py`](./scripts/hash_sebt_app_id.py). It uses the Python standard library only and is intended for downstream teams to drop into their own pipelines:

```sh
python3 docs/analytics/scripts/hash_sebt_app_id.py 'APP-2024-0001' 'TestVectorSecret_AtLeast32Bytes_!!!!'
# → ca383d90647e371547d6e66297cda8089b81fc1c5cb30da6cfcbdf744d9e2861
```

## Configuration

| Key | Required | Notes |
|---|---|---|
| `IdentifierHasher:SecretKey` | yes | At least 32 bytes UTF-8. Used for storage-side hashing (cooldown lookups, deduplication). Long-lived: rotating this key invalidates every existing stored hash. |
| `IdentifierHasher:AnalyticsSecretKey` | recommended | At least 32 bytes UTF-8. Used by `HashForAnalytics`. Kept separate from `SecretKey` so the analytics secret can be rotated freely without invalidating stored cooldown hashes. When unset, `HashForAnalytics` falls back to `SecretKey` (back-compat). |
| `STATE` | yes | Controls which state plugin is loaded. The API gates `hashedAppId` emission on `STATE=co`. Read via `IConfiguration["STATE"]` (env var or any other registered provider). |

If `IdentifierHasher:SecretKey` is missing the application fails fast at startup (`IdentifierHasherGuard`), so the field is never silently dropped. `AnalyticsSecretKey` is optional (storage key fallback) but recommended for any environment where rotation is on the table.

## Where it surfaces

| Layer | Field | Notes |
|---|---|---|
| API response | `HouseholdDataResponse.hashedAppId` | Lowercase hex string or `null` |
| Frontend data layer | `digitalData.user.hashed_app_id` | Set with `['default', 'analytics']` scope on the dashboard's `household_result` effect |
| Vendor bridges | `user.hashed_app_id` user property | Picked up by Mixpanel/Amplitude bridges via the existing `digitalData.user.*` flow |

## Companion field: `portal_id`

Stable, non-PII portal user UUID, surfaced on every state. Source: portal's own `User.Id` (Guid v7), exposed on `GET /api/auth/status` as `userId`, set on the data layer as `digitalData.user.portal_id` from `useUserDataSync`. Not hashed — UUIDs are random and carry no PII on their own. Use `hashed_app_id` to join to state program data; use `portal_id` to correlate events per portal user across pages.
