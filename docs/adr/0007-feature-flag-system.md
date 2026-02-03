# 7. Feature Flag System with Priority Tiers and State-Specific Configuration

Date: 2026-01-13

## Status

Accepted

## Context

The Summer EBT Self-Service Portal app requires a flexible feature flag system that supports multi-state deployment where each state (DC, CO, etc.) may have different feature requirements, the ability to enable/disable features without code deployments, clear precedence when multiple configuration sources exist, support for future cloud-based feature flag management via `AWS AppConfig`, and API exposure for frontend applications to query feature flag states. Without a structured feature flag system, state-specific feature rollouts would require separate code branches, deployments and/or manual tweaking of each feature flag.

## Decision

We will use **Microsoft.FeatureManagement** (https://learn.microsoft.com/en-us/dotnet/api/microsoft.featuremanagement.featuremanager?view=azure-dotnet) with a custom query wrapper service (`FeatureFlagQueryService`) that merges flags from multiple sources in a defined priority order. The system exposes flags via the `GET /api/features` REST API endpoint. State-specific configuration is loaded from `appsettings.{State}.json` files (e.g., `appsettings.dc.json`, `appsettings.co.json`) based on the `STATE` environment variable. These files are loaded using ASP.NET Core's configuration builder, which integrates with the default configuration sources and respects the priority order where environment variables override JSON file values. 

The priority order from lowest to highest: 
   - Default flags in `appsettings.json` (to represent features unique to core features that all states share, but might need to be toggled later to fit within a deployment cadence)
   - AWS AppConfig (if enabled; some states will not be on a cloud environment.  This will allow features to be flipped on or off without re-deployment)
   - State-specific JSON appsetting files, which includes FeatureManagement options (in the format of appsettings.xx.json)
   - Programmatic FF toggling with `FeatureManager` itself (Not recommended, but it would temporarily overwrite the Feature Flag until settings are changed again)


## Alternatives Considered

### Alternative 1: Environment Variables Only
**Why rejected**: Environment variables don't scale well for 50+ states. Each state would require separate environment configuration, making deployment and management complex. JSON files provide better version control and state-specific organization.

## Consequences

The priority-based system enables state-specific configuration where each state can enable/disable features independently via JSON files, provides clear priority hierarchy with predictable override behavior when multiple sources exist, supports future `AWS AppConfig` integration for cloud-based feature flag management, exposes flags via REST API for frontend consumption, includes diagnostic logging to show which source provided each flag value, and maintains version-controlled state-specific JSON files in Git. 

Downsides: Multiple configuration sources require understanding of priority order, each new state requires a new `appsettings.{State}.json` file, JSON file changes require application restart (`AWS AppConfig` addresses this for runtime updates if this is deployed on AWS though), and flags must be explicitly configured in at least one source (this would require good practice to make sure the default option is always present). 

If the app is relying on using AWS AppConfig, flag names must follow alphanumeric and underscore conventions to match `AWS AppConfig` requirements. [See More](https://docs.aws.amazon.com/appconfig/latest/userguide/appconfig-feature-flags.html) about this at the link 

## References

**Implementation Details**: 
- Service interface: `src/SEBT.Portal.Kernel/Services/IFeatureFlagQueryService.cs` (follows Clean Architecture)
- Priority merge logic: `src/SEBT.Portal.Infrastructure/Services/FeatureFlagQueryService.cs`
- REST API endpoint: `src/SEBT.Portal.Api/Controllers/FeaturesController.cs`
- Default flags: defined in the `FeatureManagement` section of `appsettings.json`
- State-specific configuration loading: `src/SEBT.Portal.Api/Program.cs` (loads `appsettings.{State}.json` based on `STATE` environment variable)
- Configuration files: `src/SEBT.Portal.Api/appsettings.json` and `appsettings.{State}.json` (e.g., `appsettings.dc.json`, `appsettings.co.json`)

**Key Documentation**:
- [Microsoft.FeatureManagement Documentation](https://github.com/microsoft/FeatureManagement-Dotnet)
- [AWS AppConfig Feature Flags](https://docs.aws.amazon.com/appconfig/latest/userguide/appconfig-feature-flags.html)

## Related ADRs
- **ADR 0002**: Adopt Clean Architecture - Feature flag system follows clean architecture patterns
- **ADR 0004**: State-based CI Architecture - State-specific JSON files integrate with state-based deployment
