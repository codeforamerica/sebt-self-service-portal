# 4. Adopt Serilog for structured logging

Date: 2025-01-27

## Status

Accepted

## Context

The Summer EBT Self-Service Portal requires comprehensive logging capabilities to support security and audit trails (tracking authentication attempts, OTP requests, and validation events), operational monitoring across 50+ state deployments, debugging and troubleshooting in production environments, and compliance requirements for regulatory and security purposes. The default ASP.NET Core logging provides basic functionality but lacks structured logging capabilities (outputs plain text strings making log analysis difficult), rich property capture for extracting specific data points, flexible output formats for multiple destinations, and production-ready features like log enrichment and filtering. With authentication flows (OTP request/validation), email sending, and rate limiting, we need to capture structured data like email addresses, error details, and request metadata in a queryable format rather than plain text logs.

## Decision

We will use **Serilog** as the structured logging framework for the SEBT Portal API, replacing the default ASP.NET Core logging via `builder.Host.UseSerilog()`.  

## Alternatives Considered

### Alternative 1: Default ASP.NET Core ILogger
**Why rejected**: While it provides basic logging, it lacks structured logging capabilities. Log entries are plain strings, making it difficult to query, filter, and analyze logs in production. Extracting specific data points (like email addresses or error codes) requires parsing log text, which is error-prone and inefficient.

## Consequences

Serilog provides structured data capture with named properties (e.g., `Email`, `Errors`) that can be queried and filtered without text parsing, enabling queryable logs that can be analyzed using tools that understand structured formats, better debugging with rich context in log entries, reliable audit trails for security and compliance requirements, and generates output that allows easy addition of additional sinks (database, cloud services, log aggregation platforms etc.) without changing application code.  We should also be more easily able to have sanitized logs as needed when PII compliance is required.

For downsides: Serilog adds additional dependencies, requires team members to understand its structured logging syntax and Serilog configuration, and has more configuration options than default logging (though JSON-based configuration is straightforward).  These aforementioned dependencies use the Apache 2.0 License.
 
## References

**Implementation Details**: 
- Serilog configuration: `src/SEBT.Portal.Api/appsettings.json`
- Serilog setup: `src/SEBT.Portal.Api/Program.cs` (lines 10-17)

**Key Documentation**:
- [Serilog Documentation](https://serilog.net/)
- [Serilog ASP.NET Core Integration](https://github.com/serilog/serilog-aspnetcore)
- [Serilog Configuration](https://github.com/serilog/serilog-settings-configuration)

## Related ADRs
- **ADR 0002**: Adopt Clean Architecture (logging is used throughout all layers)