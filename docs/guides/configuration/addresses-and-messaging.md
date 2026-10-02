---
description: Address lookup and validation, and the outbound email the portal sends.
keywords: address validation Smarty autocomplete general delivery SMTP email OTP one-time passcode
---

# Addresses and messaging

## Address validation

Mailing address changes are validated before they reach the state backend, so a household cannot redirect a card to
an address that will not deliver.

| Section | Field | Default | Meaning |
| --- | --- | --- | --- |
| `Smarty` | `Enabled` | off | Whether address verification runs at all |
| `Smarty` | `AuthId`, `AuthToken` | | Vendor credentials |
| `Smarty` | `BaseUrl` | `https://us-street.api.smarty.com` | Verification endpoint |
| `Smarty` | `TimeoutSeconds` | `20` | How long to wait before giving up |
| `AddressValidationPolicy` | `AllowGeneralDelivery` | `true` | Whether general delivery addresses are accepted |
| `AddressValidationData` | | | Reference data the policy checks against |

`AllowGeneralDelivery` defaults to permitting general delivery. That matters for households without a fixed
address, so turning it off excludes people rather than only tightening validation.

Address autocomplete in the browser uses a separate embedded key, `NEXT_PUBLIC_SMARTY_EMBEDDED_KEY`, baked in at
build time. The server-side credentials above do not cover it. Adding a browser-side call to a new vendor domain
also needs that domain in the Content Security Policy, or the request is blocked in production while working fine
locally.

## Outbound email

| Section | Purpose |
| --- | --- |
| `SmtpClientSettings` | The SMTP server used to send mail |
| `EmailOtpSenderServiceSettings` | Sender identity and behaviour for one-time passcode email |

One-time passcodes are the fallback sign-in path, so a misconfigured SMTP server locks out anyone who cannot use the
state identity provider. Local development sends to Mailpit rather than a real server; see
[Set up your environment](../local-setup/index.md).

Passcode request and validation are rate-limited separately. See [Operations](operations.md).
