---
description: The four capability decisions a state makes before anyone writes code or provisions a server, with the options running in production today.
keywords: plan program decisions procurement dependencies login OIDC single sign-on one-time passcode identity proofing Socure address validation Smarty deliverability data integrity analytics monitoring Datadog Splunk CloudWatch OpenTelemetry
---

# Plan your program

Four capabilities need a decision before anyone writes code or provisions a server. Each one below lists what is
running in production today, so you can adopt a known-good answer, and says what it takes to supply your own if none
of the listed options fit. Some of these decisions mean buying something, and the lead time on a contract is usually
longer than the work that depends on it.

If you are deploying to a cloud, the database, cache, and email sending are provisioned there as well. See
[Deploy the portal](../deploy/index.md).

> [!IMPORTANT]
> Several of these hand you a key that runs in the browser rather than on the server. Those keys are written into
> the front end when it is built, so the accounts have to exist before the build, not before the deployment. See
> [Build the portal](../build/index.md).

## Log users in

The Portal will not show anything until a guardian signs in. The Enrollment Checker deliberately needs no account at
all, so this decision only affects the Portal.

In production today:

- **Your state's single sign-on, over OpenID Connect.** Colorado uses myColorado. The portal is a standard OpenID
  Connect client, so it needs a discovery endpoint, a client ID, and a client secret from whoever runs your login
  system.
- **A one-time passcode sent by email.** Washington, DC uses this, and it is the option to reach for when there is
  no state login system to sit behind. It needs an email sender: Colorado's deployment uses Amazon SES, and any
  SMTP server works.

Bringing your own: any identity provider that speaks OpenID Connect works with configuration alone. A login system
that speaks something else is a code change.

## Proof user identities

Signing in proves someone holds an account. It does not prove they are the person whose benefit data sits behind it.
The portal keeps those separate, and will not release household data until it knows how thoroughly the person was
verified.

That confidence level is set per action: seeing an address, changing an address, opening a household, asking for a
replacement card. Every action starts at the strictest setting, and a state relaxes only the ones its own policy
allows.

In production today:

- **Your sign-in already did the work.** If your login system verifies identity and tells the portal how far it
  got, the portal takes its word for it. Colorado works this way and needs no verification vendor.
- **The portal does the work.** It puts the user through Socure. Washington, DC works this way, and needs a Socure
  account before the front end is built.

Bringing your own: the first option is vendor-neutral, so if your login system can assert a verification level, the
portal does not care who performed it. The second is not. Socure is named directly in the portal's own data model,
so swapping in a different verification vendor is development work rather than configuration.

Whichever you choose, the portal reads these settings at start-up and refuses ones that contradict each other. It
will not let you permit someone to change an address they are not allowed to see. If a bad setting arrives later,
the portal keeps the last good one rather than acting on it.
[ADR 0027](../../adr/0027-unified-id-proofing-requirements.md) explains the design, and `docs/config/ial/README.md`
in the repository lists every setting.

## Validate addresses

A mailing address is where an EBT card gets sent. An address that is mistyped, incomplete, or undeliverable does not
fail loudly: the card is printed, mailed, and returned, the family goes without benefits, and your staff handle the
fallout weeks later. The point of this decision is to stop bad addresses entering your systems in the first place,
rather than correcting them afterwards.

The portal gives you two layers, and they are independent.

**Checking against postal data.** In production today:

- **Smarty.** Addresses are suggested while the family types and checked against postal records before they are
  saved, so a typo is caught at the point of entry.
- **No vendor.** Address changes still work. The portal trims and formats what was typed and applies your own rules,
  but it cannot tell a real address from a plausible-looking one. This is the default in every state today.

**Your own rules**, applied either way and set per state:

- Reject or allow General Delivery.
- Block specific addresses outright. Both states use this for known-bad and institutional addresses.
- Rewrite street names to the spellings your downstream systems expect.
- Cap street address length so a value cannot be silently truncated by a system with a shorter field.

Bringing your own: address verification sits behind a single interface with two implementations already in the tree,
so a different vendor is a contained piece of work rather than a rewrite.

## Measure and monitor the application

Two separate concerns that are easy to confuse. One is about families and how they use the service; the other is
about whether the software is healthy.

**Product analytics.** Optional. Google Analytics, Amplitude, Mixpanel, and Siteimprove are all wired up today, and
you can run none, one, or several. Each needs its own account and browser key.

**Application monitoring.** Also optional. The portal always sends traces and metrics to a collector on the same
machine, and the collector decides where they go next. That split matters: changing monitoring tools later is a
change to the collector, not to the application.

In production today:

- Colorado sends traces to Datadog and metrics to CloudWatch.
- Washington, DC sends to Splunk.

Bringing your own: anything that accepts OpenTelemetry data works, and pointing at it is collector configuration.
Running no collector at all is a supported choice, and costs nothing: the portal starts nothing and sends nothing.

Logs are separate from both of these and are always written, to the console and to files. They can also be routed
through the collector if you would rather keep everything in one place.
[ADR 0030](../../adr/0030-web-tier-opentelemetry.md) covers how it works.

## Next

With the decisions made and the accounts open, move on to [Build the portal](../build/index.md).
