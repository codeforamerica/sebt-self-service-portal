---
description: Diagnose a state connector that fails to load or returns the wrong data.
keywords: debug error plugin not loading MEF discovery startup failure diagnose
---

# Troubleshooting

## Start-up failures

| Symptom | Cause | Correction |
| --- | --- | --- |
| `PluginAssemblyPaths missing from configuration` | The state overlay has no plugin path. | Add `PluginAssemblyPaths` to `appsettings.{state}.json`. |
| `does not implement any interface besides IStatePlugin` | A class implements the marker only. | Add one service interface to the class. |
| `implements multiple interfaces` | A class implements 2 or more service interfaces. | Split the class into one class for each interface. |
| `Connection string 'DefaultConnection' is required` | You did not configure Redis or a connection string. | Add a connection string, or configure Redis. |

## The connector does not load

The portal starts, but your state capability is absent. Work through these checks in sequence.

1. Confirm that the DLL files are in `plugins-{state}/` under the API project directory.
2. Confirm that `STATE` is set to your state code.
3. Confirm that `PluginAssemblyPaths` names the correct directory.
4. Read the log for the warning about an absent plugin directory. The loader writes a warning and continues.
5. Set the log level to `Debug` in your state overlay, then restart. The loader writes one line for each type that
   it finds.

   ```json
   { "Serilog": { "MinimumLevel": { "Default": "Debug" } } }
   ```
6. Restart the API. The portal reads assemblies at start-up only.

> [!NOTE]
> An absent directory does not stop the portal. The loader records a warning, and the portal registers a default
> for each interface. This is why an absent connector looks like a degraded application, not a crash.

## Data problems

| Symptom | Probable cause |
| --- | --- |
| The dashboard shows no children | Your `ISummerEbtCaseService` returns an empty `SummerEbtCases` list. |
| A child appears twice | The mapping creates a case and an application for the same child. |
| The address is absent for a verified user | The mapping does not return `AddressOnFile` at the correct assurance level. |
| The card status is empty | The read used `includeCardService: false`. |

Read [data mapping](data-mapping.md) for the rules that prevent the first 3 problems.

## Development without state credentials

You do not need state API credentials to build against a connector. Set `UseMockHouseholdData` to `true` in your
overlay. The API then serves fixtures from `MockHouseholdRepository`, for reads and for writes.

The method `SeedMockData()` holds the test personas. It keys them by email and by phone.

Use this setting to separate a connector defect from a portal defect.

## Known contract problems

These problems are real. You did not misread the contract.

| Problem | Effect on your connector |
| --- | --- |
| The contract holds 2 methods for the co-loaded path of DC | Return `false` and `null` from them. |
| `IStateAuthenticationService` takes a Swashbuckle type | The contract depends on a web framework type. |
| `StateMetadata` holds one property | The one mandatory interface carries almost no data. |
| The MEF attributes are inert | Copy them for consistency, or omit them. Neither choice changes the behavior. |
| `IStateMetadataService` has no consumer | Nothing in the portal calls it, so the state name that you return reaches no page. Implement it anyway, because it is the convention in both connectors. |
