# 2. Adopt clean architecture

Date: 2025-11-07

## Status

Accepted

## Context

Software products with non-trivial business logic and requirements for increased 
flexibility benefit from an architecture that maximizes modularity and testability, 
while minimizing the cost and risk of change. With this Summer EBT product, we seek to
maximize reuse across states where possible, while maintaining flexibility for state-specific
context and needs.

## Decision

We have agreed to implement the C#/.NET components of this product using the Clean 
Architecture approached popularized by Steve Smith (ardalis). Our approach is heavily
inspired by the content and resources Steve has published:
- [Clean Architecture with ASP.NET Core 8 | .NET Conf 2023 (YouTube)](https://www.youtube.com/watch?v=yF9SwL0p0Y0)
- [Clean Architecture With ASP.NET Core — Steve Smith (YouTube)](https://www.youtube.com/watch?v=qOwB8PxOqC0)
- [Clean Architecture with ASP.NET Core — Steve Smith (Blog Post)](https://ardalis.com/clean-architecture-asp-net-core/)
- [Clean Architecture Sample Repository](https://github.com/ardalis/CleanArchitecture)

## Consequences

There is a small learning curve to building with this pattern for contributor who
are unfamiliar with the approach, but overall the code for the product becomes significantly
more maintainable, with core business logic easy to unit test, and a reduced risk from change.
