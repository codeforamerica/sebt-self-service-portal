# 3. Use libphonenumber for Phone Number Normalization

Date: 2026-04-06

## Status

Accepted

## Context

The CO connector normalizes phone numbers before sending them to the CBMS API for household lookup. The original implementation stripped all non-digit characters and used `TrimStart('1')` to remove a leading US country code, accepting any string with 10+ digits.

This approach had bugs: `TrimStart('1')` removes _all_ leading 1s (not just a country code prefix), and there was no actual phone number validation — non-US numbers or garbage input with enough digits would pass through.

## Decision

Use the [libphonenumber-csharp](https://github.com/twcclegg/libphonenumber-csharp) NuGet package (C# port of Google's libphonenumber) for phone number normalization. The library is exposed via an internal `PhoneNormalizer` utility class.

The normalizer parses input assuming US region, validates with `IsValidNumberForRegion`, and returns the 10-digit national number. Invalid or non-US numbers return null.

### Alternatives considered

- **Keep the hand-rolled approach and fix the `TrimStart` bug.** Quick fix for the immediate bug, but still no real validation — doesn't catch invalid area codes, non-US numbers, or edge cases. We'd be maintaining our own parser indefinitely.
- **Use a regex-based validator.** Better than digit stripping, but [NANP](https://en.wikipedia.org/wiki/North_American_Numbering_Plan) rules are complex (no area codes starting with 0 or 1, no 555 exchange codes for real numbers, etc.). A regex that covers all cases would be fragile and hard to maintain.

## Consequences

- Phone normalization correctly handles all common US formats: `(818) 555-1234`, `+1-818-555-1234`, `818.555.1234`, etc.
- Non-US numbers and invalid US numbers are rejected rather than silently passed to the backend.
- The `libphonenumber-csharp` package (~2 MB) is added to the connector's plugin output. It has no transitive dependencies and is widely used (Google's reference implementation).
- Other connectors can adopt the same library if they need phone normalization in the future.
