# 7. Multi-state Plugin Approach

Date: 2026-01-07

## Status

Proposed

## Context

The Summer EBT self-service portal should maximize code reuse for core product features across the tech stack to promote ease of scalability to additional states in the future. There will be some dynamic behavior that varies from state to state, and much, if not most, of this can be accomplished in a scalable way by building with good modularity, appropriate abstractions, and configurability.

However, we expect backend systems of record for SEBT case data to be unique to each state. Some states (e.g., Colorado) may provide API access to read and write this data, while others (e.g., Washington, D.C.) may provide more direct access to databases. While a standardized REST API contract may be desirable for most implementing states in the long-run, at this time, we need the ability to support more varied approaches.

We intend to use a plugin architecture, defining the plugin contract in a separate git repository from the core product, and creating plugin implementations for each state in distinct git repositories.

With this foundation, a key question is how to manage the dependencies between these repositories.

### Alternatives Considered

1. **NuGet Package** - The contract definition for the plugins will be built and published as a NuGet package. Each plugin project, as well as the main application project, can then reference this NuGet package to use the contracts defined within it.
   - Simplest integration method. NuGet is the standard package manager for .NET.
   - Open question around whether to publish as a GitHub package vs defining a local NuGet store (using a directory). The latter option would allow for a tighter development inner loop when iterating locally on the plugins.

2. **Git Submodules** - The contract definition for the plugins will be referenced by the plugin implementations and main application projects using a git submodule reference.
   - Integration accomplished via hard project references, for tightest development inner loop.
   - Frequent changes to referenced repository makes usage error prone. Complexity with branching. Hard to do correctly.

## Decision

We have decided to use NuGet for integration of the plugin contracts module. This approach uses standard tooling for .NET and is the least error prone. Different approaches to package publishing have varying impacts on the inner loop development workflow, but this is outweighed by the simplicity of the approach and avoidance of errors likely with the submodule approach.

## Consequences

If successful, this approach will balance speed of iteration with ease of correctness when developing plugins for state-specific integration code for the Summer EBT self-service portal. Additional documentation will need to be added to the project README to provide guidance on the plugin development process, especially for new team members.

## References

- [Setting up local NuGet feeds](https://learn.microsoft.com/en-us/nuget/hosting-packages/local-feeds)
- [Git submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules)
- [GitHub packages](https://docs.github.com/en/packages)
- [Managed Extensibility Framework for .NET](https://learn.microsoft.com/en-us/dotnet/framework/mef/)
