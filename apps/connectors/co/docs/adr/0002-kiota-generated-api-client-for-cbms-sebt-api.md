# 2. Kiota-generated API client for CBMS SEBT API

Date: 2026-02-13

## Status

Accepted

## Context

The Colorado connector integrates with CDHS's CBMS SEBT API — a REST API for checking enrollment, retrieving account details, and updating student information. We would like a typed HTTP client in C# to call this API.

The CBMS API's OpenAPI spec uses generic, auto-generated schema names (`type`, `type_1`, `type_2`, etc.) and defines nested objects inline rather than as reusable schemas. Generating a client directly from this spec would produce C# classes named `Type`, `Type1`, `Type2` — unusable from a readability and maintainability standpoint.

### Alternatives Considered

1. **Hand-written HTTP client** — Build `HttpClient`-based service classes manually, with hand-authored request/response DTOs.
   - Full control over naming and structure.
   - No tooling dependencies.
   - Tedious to maintain as the API evolves; easy for the code to drift from the spec without anyone noticing.

2. **Kiota with spec transformation** — Use Microsoft's [Kiota](https://learn.microsoft.com/en-us/openapi/kiota/) code generator, preceded by a transformation step that renames schemas and extracts inline objects into top-level definitions.
   - Generated client stays in sync with the spec by design.
   - Transformation pipeline makes the generated code readable despite the spec's generic naming.
   - Kiota is Microsoft's recommended approach for OpenAPI client generation in .NET.

## Decision

We use **Kiota** to generate a strongly-typed C# API client, preceded by a **spec transformation pipeline** that produces a clean OpenAPI spec from the raw CBMS spec. The pipeline lives in the `openapi/` directory within the generated client project (`src/SEBT.Portal.StatePlugins.CO.CbmsApi/openapi/`) and works as follows:

1. **`transform-spec.sh`** runs `transform_spec.py`, which reads `api-spec.original.yaml` and writes `api-spec.yaml` with two categories of changes:
   - **Schema renames** — Maps generic names to descriptive ones (e.g., `type` → `CheckEnrollmentRequest`, `type_1` → `CheckEnrollmentResponse`).
   - **Inline object extraction** — Promotes nested inline objects to top-level schemas (e.g., the `stdntDtls` array items become `CheckEnrollmentStudentDetail`, the `addr` object becomes `Address`).

2. **`generate-client.sh`** invokes `dotnet kiota generate` against the transformed spec, producing the `SEBT.Portal.StatePlugins.CO.CbmsApi` project.

### Guarding against spec drift with serialization tests

Because the transformation step sits between the raw spec and the generated code, there is a risk that changes to the upstream spec could silently break the mapping between JSON wire format and C# model properties. To catch this, we maintain **serialization/deserialization tests** (`CbmsApi/` folder in the test project) that:

- **Deserialize** the spec's example JSON payloads into the generated model types and assert every field.
- **Serialize** populated model instances to JSON and verify the wire-format field names using `System.Text.Json.JsonDocument`.
- **Assert `AdditionalData` is empty** after deserialization. Kiota models silently capture any unmapped JSON fields in an `AdditionalData` dictionary. By asserting this dictionary is empty, we detect when the spec introduces new attributes that don't yet have corresponding typed properties on the generated model — prompting regeneration.

These tests use `KiotaJsonSerializer` from `Microsoft.Kiota.Abstractions.Serialization`, with JSON serializer/deserializer factories registered via a module initializer (`KiotaSerializerSetup.cs`).

## Consequences

- **Spec updates require a defined workflow:** update `api-spec.original.yaml`, run the transform, regenerate the client, and update the test fixtures. The serialization tests enforce this discipline — skipping a step causes test failures.
- **The transformation is a maintenance surface:** if the upstream spec adds new endpoints or restructures existing schemas, `transform_spec.py` may need new rename/extraction entries. The rename and extraction mappings are declared as simple data structures at the top of the file to keep this straightforward.
- **The `AdditionalData` assertions provide an early warning** for additive spec changes (new fields). These aren't breaking changes, but surfacing them ensures we consciously decide whether to regenerate or defer.

## References

- [Microsoft Kiota documentation](https://learn.microsoft.com/en-us/openapi/kiota/)
- [ADR-0007 in main repo: Multi-state Plugin Approach](https://github.com/codeforamerica/sebt-self-service-portal/blob/main/docs/adr/0007-multi-state-plugin-approach.md)
