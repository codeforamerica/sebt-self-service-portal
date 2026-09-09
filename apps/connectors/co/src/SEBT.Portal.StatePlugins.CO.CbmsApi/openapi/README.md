# OpenAPI Spec and Transformation Pipeline

This directory contains the CBMS SEBT API OpenAPI spec and the tooling that transforms it into a form suitable for Kiota client generation.

## Contents

| File | Purpose |
|------|---------|
| `api-spec.original.yaml` | Raw OpenAPI spec as provided by the CBMS team |
| `api-spec.yaml` | Transformed spec with descriptive schema names |
| `transform-spec.sh` | Runs the Python transformation script |
| `transform_spec.py` | Renames generic schemas and extracts inline objects |
| `generate-client.sh` | Invokes Kiota to regenerate the C# client |

## Why transform the spec?

The CBMS API spec uses generic, auto-generated schema names (`type`, `type_1`, `type_2`, etc.) and defines nested objects inline. Without transformation, Kiota would produce C# classes named `Type`, `Type1`, `Type2` — unusable from a readability standpoint.

The transformation does two things:

1. **Renames schemas** to descriptive names (e.g., `type` → `CheckEnrollmentRequest`, `type_1` → `CheckEnrollmentResponse`). The mappings are declared in `SCHEMA_RENAMES` at the top of `transform_spec.py`.

2. **Extracts inline objects** into top-level schemas (e.g., the `stdntDtls` array items become `CheckEnrollmentStudentDetail`, the `addr` object becomes `Address`). The extractions are declared in `EXTRACTIONS` at the top of `transform_spec.py`.

## Handling an updated spec

When the CBMS team provides a new version of the API spec:

### 1. Replace the original spec

Copy the new spec over `api-spec.original.yaml`.

### 2. Update the transformation mappings (if needed)

If the new spec adds or renames schemas, update the `SCHEMA_RENAMES` and/or `EXTRACTIONS` lists in `transform_spec.py`. If the spec only changes field values or adds fields to existing schemas, no mapping changes are needed.

### 3. Run the transformation

```bash
./transform-spec.sh
```

This reads `api-spec.original.yaml` and writes the transformed `api-spec.yaml`.

### 4. Regenerate the client

```bash
./generate-client.sh
```

This invokes `dotnet kiota generate` with `--clean-output`, which wipes and regenerates all C# files in the parent project directory. The script automatically backs up and restores this `openapi/` directory and the `.csproj` during regeneration.

### 5. Update the serialization tests

The test project (`SEBT.Portal.StatePlugins.CO.Tests/CbmsApi/`) contains serialization tests whose JSON fixtures are based on the spec's example payloads. Update these fixtures to match the new spec examples, then run:

```bash
dotnet test src/SEBT.Portal.StatePlugins.CO.Tests/
```

If the spec added new fields, the `Assert.Empty(result.AdditionalData)` assertions will fail — signaling that the generated models don't yet have typed properties for those fields. Regenerating the client (step 4) resolves this.

If the spec changed or removed fields, the value assertions will fail — pointing you to exactly what changed.

## See also

- [ADR-0002: Kiota-generated API client for CBMS SEBT API](../../../docs/adr/0002-kiota-generated-api-client-for-cbms-sebt-api.md)
