# CBMS mock responses

These JSON files provide mock responses for the CBMS SEBT API when `Cbms:UseMockResponses` is enabled. They are used for:

- **Integration tests** — Run `dotnet test` with `Cbms__UseMockResponses=true` for CI or local runs without sandbox credentials.
- **Local development** — Set `Cbms:UseMockResponses=true` in your host app's config to exercise the Colorado plugin without CBMS credentials or network access.

Data is based on examples from the CBMS API OpenAPI spec. Edit these files to add scenarios or change responses; they are embedded as resources and loaded at runtime.
