# 11. Plugin DI Bridge — Replace MEF Composition with DI-Based Instantiation

Date: 2026-04-06

## Status

Accepted

## Context

The multi-state plugin system (ADR-0007) uses MEF (System.Composition) for both assembly loading and plugin instantiation. MEF's `[ImportingConstructor]` only injects other MEF-exported types, which prevents plugin constructors from receiving DI-registered services like `HybridCache`. As the portal's service layer grows, plugins increasingly need access to services managed by the DI container.

## Decision

Replace MEF's composition and instantiation with DI-based instantiation via `ActivatorUtilities`, while preserving MEF's assembly-loading logic.

Key details:

- **Assembly scanning preserved.** The existing logic loads assemblies from `plugins-{state}/` directories and scans for types implementing `IStatePlugin`.
- **DI instantiation.** Discovered plugin types are registered as singleton factories that use `ActivatorUtilities.CreateInstance`, giving plugin constructors access to any DI-registered service.
- **Health check exception.** `IStateHealthCheckService` plugins are eagerly instantiated via a temporary `ServiceProvider` because ASP.NET health check registration requires concrete instances at startup. This is a known limitation — the temporary provider cannot share state with the final container.
- **MEF attributes are inert.** `[Export]`, `[ExportMetadata]`, and `[ImportingConstructor]` remain on plugin classes for now but are not used. They will be removed in a future cleanup once the DI bridge is proven stable.

## Consequences

- Plugin constructors can now depend on any DI-registered service, unblocking caching and other cross-cutting concerns.
- The temporary service provider for health checks is a documented trade-off; health check plugins should remain stateless.
- System.Composition remains a transitive dependency until the inert attributes are removed.
- Full design details are in the CO connector repo's specification documents.
