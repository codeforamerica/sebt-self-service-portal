---
description: How the portal finds and calls a state connector, what the contract covers, and what keeps the two sides in step.
keywords: state connector plugin contract interfaces discovery runtime MEF System.Composition canonical model mapping enum parity reads writes Colorado DC
---

# How the connector works

The connector is the one component each state writes. Everything the portal knows about a particular state's systems
lives there, which is why adding a state does not mean changing the portal.

## How the portal finds it

The portal does not reference your connector at compile time. It loads it at run time from a directory belonging to
the configured state, and the `STATE` environment variable is what decides which one.

This is what makes the connector genuinely separable. Colorado keeps its connector in this repository, next to the
portal. Washington, DC keeps its own in a repository of its own, built against a published contract and dropped in
as compiled files. Both arrangements work because neither the portal nor the connector needs the other's source.

## What the contract covers

A connector is a set of services the portal asks for by name. Not every one has to do real work, and a state
adopting only part of the portal implements only the parts it uses.

| Service | What the state supplies |
| --- | --- |
| <xref:SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService> | Household lookup by identifier. The main read path. |
| <xref:SEBT.Portal.StatesPlugins.Interfaces.IEnrollmentCheckService> | Enrollment matching for the unauthenticated check. |
| <xref:SEBT.Portal.StatesPlugins.Interfaces.IAddressUpdateService> | Writes a mailing address change back to the state's systems. |
| <xref:SEBT.Portal.StatesPlugins.Interfaces.ICardReplacementService> | Dispatches a replacement card request and updates whatever cooldown the state tracks. |
| <xref:SEBT.Portal.StatesPlugins.Interfaces.IStateMetadataService> | State metadata the portal displays. |
| <xref:SEBT.Portal.StatesPlugins.Interfaces.IStateHealthCheckService> | Health checks for the state's own dependencies, folded into the API's health endpoint. |
| <xref:SEBT.Portal.StatesPlugins.Interfaces.IStateAuthenticationService> | Security options matching the state's authentication scheme. |

Reads are the default. Only three operations write anything back: a mailing address, a card replacement request, and
contact preferences. Adopting the portal does not mean granting broad write access to a system of record.

## Translating to the portal's model

The portal has one internal model of a household, and it does not resemble any state's schema. Turning yours into it
is the connector's real work, and it is where most of the effort goes.

The gap is genuine rather than cosmetic. The portal separates a case, meaning one child with issued benefits, from
an application, meaning a guardian's submission covering one or more children. Most children have a case and no
application. Washington, DC draws a similar line. Colorado represents everything as an application, so its connector
splits one record into the portal's two.

Two states with completely different source systems produce identical output for the portal, which is what lets the
rest of the application stay state-neutral.

## What keeps both sides in step

A contract shared across repositories can drift, and the failure mode is quiet rather than loud. Several values
cross the boundary as numbers rather than names, so reordering a list on one side without the other does not throw
an error. It silently changes what a value means, and an approved application starts reading as a pending one.

Tests compare both sides by name and by value, so a change to either declaration fails the build rather than
reaching a family's screen. Further tests load each state's real connector against a running API and check that it
answers, so a connector that compiles but cannot be discovered is caught before release.

## Where to go next

- [Build a state connector](../state-connector/index.md) walks through implementing the contract, with the data
  mapping in full.
- [Data flows](data-flows.md) covers what happens on either side of the connector.
- [ADR 0023](../../adr/0023-multi-state-plugin-approach.md) records why the plugin approach was chosen.
