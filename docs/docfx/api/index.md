# .NET API Reference

Reference documentation for the application's C# types. This page is automatically generated based on the source by `docfx metadata`. 

The description of each page is the `///` comment from the code. If a type reads as undocumented here, it is undocumented in the source.

## The Portal API

`SEBT.Portal.Api` serves both front end applications:

| Front end | Calls | Handled by |
| --- | --- | --- |
| Portal (`SEBT.Portal.Web`) | Authenticated household, card, and address routes | `SEBT.Portal.UseCases` handlers for auth and households |
| Enrollment Checker (`SEBT.EnrollmentChecker.Web`) | Unauthenticated enrollment check route | <xref:SEBT.Portal.Api.Controllers.EnrollmentCheck.EnrollmentCheckController> into <xref:SEBT.Portal.UseCases.EnrollmentCheck> |


## Layers

- <xref:SEBT.Portal.Api> |  ASP.NET core entry point (controllers, middleware, plugin loading) - used by Portal and Enrollment checker
- <xref:SEBT.Portal.UseCases> | Application layer command/query handlers (auth, households)
- <xref:SEBT.Portal.Core> | Domain models, service interfaces, exceptions, settings
- <xref:SEBT.Portal.Infrastructure> | DB context and migrations, repositories, service implementations, external integrations
- <xref:SEBT.Portal.Kernel> | Base classes, ASP.NET extensions
- <xref:SEBT.Portal.StatesPlugins.Interfaces>: the contract every state connector implements
