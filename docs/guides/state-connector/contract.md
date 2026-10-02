---
description: The seven service interfaces a state connector implements, and which are required.
keywords: IStatePlugin interfaces contract API implement services required optional
---

# The contract

The contract is the project `SEBT.Portal.StatesPlugins.Interfaces`. It holds 7 service interfaces. The .NET API
reference gives the full signatures. This page tells you which interfaces you need and what each one does.

## The interfaces

No interface is mandatory for start-up. The portal calls `TryAddSingleton` to register a default for 6 of the 7
interfaces. If you do not implement one of those 6, the portal starts and degrades that capability.

`IStateMetadataService` has no default. It also has no consumer in the portal, so its absence causes no failure
today. Implement it anyway. Both existing connectors do, and a future consumer would then find it.

| Interface | Default exists | Effect if you do not implement it |
| --- | --- | --- |
| `ISummerEbtCaseService` | Yes | The dashboard shows no household data. |
| `IEnrollmentCheckService` | Yes | The Enrollment Checker cannot give an answer. |
| `IAddressUpdateService` | Yes | Address updates are not available. |
| `ICardReplacementService` | Yes | Card replacement requests are not available. |
| `IStateHealthCheckService` | Yes | Health output shows no state dependency. |
| `IStateAuthenticationService` | Yes | The portal uses its default configuration. |
| `IStateMetadataService` | No | Nothing fails today, because nothing calls it. |

A complete connector implements these 5 interfaces:

1. `IStateMetadataService`
2. `ISummerEbtCaseService`
3. `IEnrollmentCheckService`
4. `IAddressUpdateService`
5. `ICardReplacementService`

Add `IStateHealthCheckService` for operational visibility. Colorado implements all 7 interfaces.

## Rules for each class

The portal enforces 4 rules at start-up. A mistake gives a crash or an absent capability. It does not give a compiler
error.

| Rule | Detail |
| --- | --- |
| Discovery is by reflection | The loader registers each concrete type that is assignable to `IStatePlugin`. |
| One service interface for each class | A class implements `IStatePlugin` and exactly one service interface. |
| Constructors use DI | The loader uses `ActivatorUtilities`, so a constructor can take `IConfiguration` or `ILoggerFactory`. |
| Health checks load early | The portal creates health check classes during service registration. |

> [!WARNING]
> The MEF attributes on the Colorado classes are inert. `[Export]`, `[ExportMetadata]`, and `[ImportingConstructor]`
> have no effect. Nothing reads `ExportMetadata("StateCode", "CO")`. Do not expect an attribute to select your
> connector. The loader uses reflection only.

<details>
<summary>Why one interface for each class</summary>

The loader inspects the interfaces of each type. It needs exactly one service interface to register against. Zero
interfaces gives the error `does not implement any interface besides IStatePlugin`. Two or more interfaces gives the
error `implements multiple interfaces`. Both errors are an `InvalidOperationException` at start-up.

This rule is the reason that Colorado has 7 small classes and no single large class.

</details>

<details>
<summary>The limit on health check dependencies</summary>

`ConfigureHealthChecks` needs an `IHealthChecksBuilder`. The builder exists only during service registration, so the
loader creates health check classes early. To resolve their constructors, the loader builds a temporary
`IServiceProvider`.

The temporary provider has its own singleton scope. Health check classes today take `IConfiguration` and
`ILoggerFactory` only, and both are already complete at that point. A health check that takes a service with shared
mutable state gets a different instance than the application uses. A Redis-backed `HybridCache` is one example.

The loader records this limit in a comment. Correct it there if your connector needs such a dependency.

</details>

## ISummerEbtCaseService

This interface holds 4 methods. Two of them serve the co-loaded path of DC. The doc comments in the contract say so.
The parameter comments name warehouse fields of DC, such as `IC`, `PortalUUID`, and `SocureUUID`.

| Method | What a new state does |
| --- | --- |
| `GetHouseholdByIdentifierAsync` | Implement it. This is the preferred entry point. |
| `GetHouseholdByGuardianEmailAsync` | Implement it. It can call the method above. |
| `TryMatchCoLoadedGuardianByBenefitIdAndDobAsync` | Return `false`, unless your state co-loads with SNAP or TANF. |
| `GetHouseholdByBenefitIdentifierAndDobAsync` | Return `null`, unless your state co-loads with SNAP or TANF. |

> [!IMPORTANT]
> Obey the `piiVisibility` and `identityAssuranceLevel` arguments on each read. They tell you which data the caller
> can receive. The authorization model of the portal assumes that your connector obeys them. See
> [ADR 0027](../../adr/0027-unified-id-proofing-requirements.md).

## The contract package

In-repo connectors use a `ProjectReference`. A command of `dotnet build SEBT.slnx` then builds the contract and the
connector together.

Out-of-tree connectors restore the contract as a NuGet package from `~/nuget-store/`. The DC connector uses this
path. The property `StateConnectorInterfacesVersion` in the root `Directory.Build.props` sets the version.
[ADR 0021](../../adr/0021-lock-ecr-image-contract.md) tells why the API container image builds the contract from
source instead.
