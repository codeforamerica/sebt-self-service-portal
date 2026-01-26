# 7. Adopt **Bogus** and factory pattern for test data generation

Date: 2025-01-13

## Status

Accepted

## Context

The SEBT Portal requires realistic test data for development, testing, and database seeding (both for developer and QA purposes). Manually creating test data is time-consuming, error-prone, and produces inconsistent data that doesn't reflect real-world scenarios.  There's also missed opportunties in testing things that wouldn't be caught in a more manual creation, like validations not tied to DB-level constraints.  We need a consistent, maintainable approach to generate users with various states (i.e co-loaded, non-co-loaded, different ID proofing statuses) that can be reused across unit tests, integration tests, and database seeding (for QA/Product review).

## Decision

We will use **Bogus** for generating realistic fake data and implement a factory pattern (the first iteration being `UserFactory`) in the Infrastructure layer to centralize test (both for testing ) data creation logic. For example, the `UserFactory` provides methods like `CreateUser()`, `CreateCoLoadedUser()`, `CreateNonCoLoadedUser()`, and `CreateUserWithEmail()` for generating `User` domain models and `UserEntity` database entities with realistic data.  This is proposed as a model for generating test data for future models.

## Alternatives Considered

### Alternative 1: Manual test data creation
**Why rejected**: Requires repetitive boilerplate code in every test, leading to inconsistencies and maintenance burden. Changes to models require updating multiple test files.

### Alternative 2: Test-specific factories in test project
**Why rejected**: Database seeding in the Infrastructure layer also needs test data, creating duplication. Moving the factory to Infrastructure allows reuse across tests and seeding while maintaining Clean Architecture expectations.

## Consequences

Bogus provides realistic, randomized data generation (names, emails, dates) that improves test coverage and makes tests more robust in what it's testing. The factory pattern centralizes data creation logic, ensuring consistency across tests and seeding, reduces duplication, and simplifies maintenance when there are model changes. The factory is located in Infrastructure to support both testing and database seeding.

For downsides: Bogus adds a dependency (MIT License), and team members need to understand the factory methods and pattern.

## References

**Implementation Details**: 
- Factory: `src/SEBT.Portal.Infrastructure/Helpers/UserFactory.cs`
- Seeding usage: `src/SEBT.Portal.Infrastructure/Services/DatabaseSeeder.cs`
- Test usage: `test/SEBT.Portal.Tests/Unit/Repositories/DatabaseUserRepositoryTests.cs`

**Key Documentation**:
- [Bogus Documentation](https://github.com/bchavez/Bogus)
- [Factory Pattern](https://refactoring.guru/design-patterns/factory-method)

## Related ADRs

- **ADR 0002**: Adopt Clean Architecture (factory located in Infrastructure layer)
