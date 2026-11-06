# CBMS mock responses

These JSON files provide mock responses for the CBMS SEBT API when the `use_mock_cbms_responses` feature flag is enabled. They are used for:

- **Integration tests** — Run `dotnet test` with `FeatureManagement__use_mock_cbms_responses=true` for CI or local runs without sandbox credentials.
- **Local development** — Enable `use_mock_cbms_responses` in the host app's `FeatureManagement` section to exercise the Colorado plugin without CBMS credentials or network access.

Data is based on examples from the CBMS API OpenAPI spec. Edit these files to add scenarios or change responses; they are embedded as resources and loaded at runtime.
