# Hashed SEBT App ID — Analytics Spec

This document is the contract between the SEBT portal and downstream analytics consumers (CO program staff, CfA analysts, OIT). It defines exactly how a SEBT App ID is hashed for emission to vendor analytics tools so the same digest can be reproduced by external pipelines and used as a join key against state program data.

## Scope

The hashed SEBT App ID, and the hashed id lists described below, are emitted today for **Colorado** dashboards. Other states are unaffected — the API simply does not populate the fields for them.

## Algorithm

```
hashed_app_id = HMAC_SHA256(secret, sebt_app_id_utf8)
```

- **Algorithm:** HMAC-SHA256
- **Key:** the configured shared secret loaded via `IdentifierHasher:SecretKey`
- **Message:** the SEBT App ID, encoded as UTF-8 with no normalization
- **Output encoding:** lowercase hexadecimal, 64 characters

### Which App ID `hashed_app_id` carries

A household can hold several App IDs, so `hashed_app_id` hashes one chosen the same way on every page load:

1. The lowest application number (ordinal string sort), when the household has any applications. This is the value the field carried before the id lists were added.
2. Otherwise, the lowest App ID across the household's children. Households whose children were all directly certified fall into this case.

### Id lists: `hashed_app_ids` and `hashed_case_ids`

```
hashed_app_ids  = join(",", sort(unique(first16(HMAC_SHA256(secret, sebt_app_id_utf8)))))
hashed_case_ids = join(",", sort(unique(first16(HMAC_SHA256(secret, sebt_chld_id_utf8)))))
```

- **`hashed_app_ids`:** one entry per distinct CBMS `sebtAppId` in the household, whatever the eligibility source (directly certified children included).
- **`hashed_case_ids`:** one entry per distinct CBMS `sebtChldId` (the case id, unique per child per season), including children known only through a pending application.
- **Entry:** the first 16 characters of the same digest as above, using the same key.
- **Order:** ordinal sort, comma-joined with no spaces.
- **Message:** the id as a base-10 string (CBMS sends integers), e.g. `1200736`.
- **Excluded:** rows with `stdntEligSts=DD` (denied duplicates) never reach the portal's household data, so their ids never appear.
- **Why prefixes:** Mixpanel truncates string properties at 255 bytes. Sixteen characters per entry keeps a household of up to 15 ids under that limit. To join, compute the full digest of the id you hold and compare its first 16 characters.

### Input normalization

Use the SEBT App ID **exactly as returned** by the source system. Do not trim, lowercase, or strip dashes/spaces. Two notes:

- The portal's storage-side hasher (`IIdentifierHasher.Hash`) **does** normalize identifiers (trim, dash/space stripping) for cooldown lookups. That method must not be used for analytics — the public spec relies on raw input so external consumers can reproduce the digest from the value they hold.
- A separate method, `IIdentifierHasher.HashForAnalytics`, implements this spec and is the only path that should feed `digitalData.user.hashed_app_id`, `hashed_app_ids`, and `hashed_case_ids`.

### Null / empty input

`HashForAnalytics` returns `null` for null, empty, or whitespace-only input, and the id lists skip such ids. When a household has no id of a kind, the API returns that field as `null`. The frontend then clears the matching `digitalData.user.*` value with `undefined`, and the data layer leaves undefined values off events. Analytics therefore sees no field at all, rather than an empty value or a digest of an empty string, and one household's ids never carry over to the next household's events.

## Test vector

A reproducible vector that every implementation (backend C#, reference Python, downstream pipelines) must match. **The secret below is for vector verification only — never use it in any environment.**

| Field | Value |
|---|---|
| Secret (32+ bytes UTF-8) | `TestVectorSecret_AtLeast32Bytes_!!!!` |
| Input (SEBT App ID) | `APP-2024-0001` |
| Expected digest | `ca383d90647e371547d6e66297cda8089b81fc1c5cb30da6cfcbdf744d9e2861` |
| Expected list entry (first 16) | `ca383d90647e3715` |

The backend test `IdentifierHasherTests.HashForAnalytics_TestVector_MatchesPublishedReference` asserts this exact triplet, so any change to the algorithm fails CI rather than silently desynchronizing external consumers.

## Reference implementation

A standalone reference script lives at [`scripts/hash_sebt_app_id.py`](./scripts/hash_sebt_app_id.py). It uses the Python standard library only and is intended for downstream teams to drop into their own pipelines:

```sh
python3 docs/analytics/scripts/hash_sebt_app_id.py 'APP-2024-0001' 'TestVectorSecret_AtLeast32Bytes_!!!!'
# → ca383d90647e371547d6e66297cda8089b81fc1c5cb30da6cfcbdf744d9e2861
```

The script hashes a `sebtChldId` the same way. For list entries, take the first 16 characters of its output.

## Configuration

| Key | Required | Notes |
|---|---|---|
| `IdentifierHasher:SecretKey` | yes | At least 32 bytes UTF-8. Used for storage-side hashing (cooldown lookups, deduplication). Long-lived: rotating this key invalidates every existing stored hash. |
| `IdentifierHasher:AnalyticsSecretKey` | recommended | At least 32 bytes UTF-8. Used by `HashForAnalytics`. Kept separate from `SecretKey` so the analytics secret can be rotated freely without invalidating stored cooldown hashes. When unset, `HashForAnalytics` falls back to `SecretKey` (back-compat). |
| `STATE` | yes | Controls which state plugin is loaded. The API gates emission of `hashedAppId`, `hashedAppIds`, and `hashedCaseIds` on `STATE=co`. Read via `IConfiguration["STATE"]` (env var or any other registered provider). |

If `IdentifierHasher:SecretKey` is missing the application fails fast at startup (`IdentifierHasherGuard`), so the field is never silently dropped. `AnalyticsSecretKey` is optional (storage key fallback) but recommended for any environment where rotation is on the table.

## Where it surfaces

| Layer | Field | Notes |
|---|---|---|
| API response | `HouseholdDataResponse.hashedAppId` | Lowercase hex string or `null` |
| API response | `HouseholdDataResponse.hashedAppIds`, `hashedCaseIds` | Comma-joined 16-character entries, or `null` |
| Frontend data layer | `digitalData.user.hashed_app_id`, `hashed_app_ids`, `hashed_case_ids` | Set with `['default', 'analytics']` scope wherever household data loads, and cleared when the API returns `null` |
| Vendor bridges | `hashed_app_id`, `hashed_app_ids`, `hashed_case_ids` event properties | The data layer merges them into every tracked event's payload, which the Mixpanel/Amplitude bridges forward as-is |

### Known limits

- **Page views and vendor autocapture** don't carry these fields. Only events sent through `trackEvent` do.
- **Events before household data loads** (sign-in, ID proofing) carry none, because the household isn't known yet.
- **Households with 16 or more ids of one kind** exceed Mixpanel's 255-byte limit even with prefixes, and the list is cut off.

## Companion field: `portal_id`

Stable, non-PII portal user UUID, surfaced on every state. Source: portal's own `User.Id` (Guid v7), exposed on `GET /api/auth/status` as `userId`, set on the data layer as `digitalData.user.portal_id` from `useUserDataSync`. Not hashed — UUIDs are random and carry no PII on their own. Use `hashed_app_id` to join to state program data; use `portal_id` to correlate events per portal user across pages.
