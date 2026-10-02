using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Produces the OpenAPI document that the documentation site renders as its REST reference.
///
/// The document is written to the path in the <c>SEBT_OPENAPI_OUTPUT</c> environment variable,
/// which `pnpm docs:spec` sets. With the variable unset the tests still run and assert the
/// document generates, so a change that breaks Swagger generation fails the normal test suite
/// rather than waiting for someone to build the docs.
///
/// Generating through the test host rather than Swashbuckle's CLI is deliberate. Both
/// `dotnet swagger tofile` and the build-time `GetDocument` tool start the app through
/// <c>HostFactoryResolver</c>, which runs `Program.Main` far enough to reach the plugin
/// registration in <c>Program.cs</c>; that throws without `PluginAssemblyPaths`, and supplying
/// one makes the tool load the plugin directory into its own assembly load context, where
/// `System.Composition.Runtime` collides with the copy the tool already holds.
/// <see cref="PortalWebApplicationFactory"/> configures the host correctly and has no such
/// collision.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class OpenApiDocumentExportTests : IClassFixture<PortalWebApplicationFactory>
{
    /// <summary>Swagger document name registered by <c>AddSwaggerGen</c>.</summary>
    private const string DocumentName = "v1";

    /// <summary>Set by `pnpm docs:spec` to the file the documentation site reads.</summary>
    private const string OutputPathVariable = "SEBT_OPENAPI_OUTPUT";

    private readonly PortalWebApplicationFactory _factory;

    public OpenApiDocumentExportTests(PortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The site renders the document with RapiDoc, which reads OpenAPI 3 directly, so the export
    /// keeps the version the API itself serves. An earlier approach serialized down to Swagger 2.0
    /// to satisfy docfx's built-in REST processor; that processor is the only thing that needed
    /// the downgrade, and it is no longer in the path.
    /// </summary>
    [Fact]
    public void Document_SerializesAsOpenApi3_AndDescribesPortalEndpoints()
    {
        var json = GenerateOpenApiDocument();

        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        Assert.StartsWith("3.", root.GetProperty("openapi").GetString());

        var paths = root.GetProperty("paths");
        Assert.True(
            paths.TryGetProperty("/api/features", out _),
            "Expected /api/features in the generated document. Routes are lowercased by RouteOptions, "
                + "so a casing change in the controller route would show up here.");
    }

    /// <summary>
    /// The controllers carry <c>&lt;summary&gt;</c> comments and <c>&lt;response&gt;</c> tags, but
    /// they only reach the document when SwaggerGen is told to read the generated XML documentation
    /// file. Without that wiring every endpoint renders as a bare method name, which is the
    /// difference between a reference and a route list.
    /// </summary>
    [Fact]
    public void Document_CarriesXmlDocCommentsFromControllers()
    {
        var getFeatures = GetDocument().Paths["/api/features"].Operations[OperationType.Get];

        Assert.False(
            string.IsNullOrWhiteSpace(getFeatures.Summary),
            "GetFeatureFlags has a <summary> comment; an empty summary means SwaggerGen is not "
                + "reading the XML documentation file.");
        Assert.Contains("feature flag", getFeatures.Summary, StringComparison.OrdinalIgnoreCase);

        Assert.False(
            string.IsNullOrWhiteSpace(getFeatures.Responses["200"].Description),
            "The <response code=\"200\"> tag should supply the response description.");
    }

    /// <summary>
    /// Every operation carries a tag, because the site's search index and the RapiDoc navigation
    /// both group by it. An untagged operation would land in a stray "default" group.
    /// </summary>
    [Fact]
    public void EveryOperation_IsTagged()
    {
        var untagged = GetDocument().Paths
            .SelectMany(path => path.Value.Operations.Select(op => (Path: path.Key, op.Key, op.Value)))
            .Where(entry => entry.Value.Tags is null || entry.Value.Tags.Count == 0)
            .Select(entry => $"{entry.Key} {entry.Path}")
            .ToList();

        Assert.True(
            untagged.Count == 0,
            $"Operations without a [Tags(...)] attribute: {string.Join(", ", untagged)}");
    }

    /// <summary>
    /// Writes the document for the documentation site. Does nothing unless `pnpm docs:spec` set the
    /// output path, so an ordinary `dotnet test` run leaves no files behind.
    /// </summary>
    [Fact]
    public void Document_IsWrittenToDocsSite_WhenOutputPathConfigured()
    {
        var outputPath = Environment.GetEnvironmentVariable(OutputPathVariable);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, GenerateOpenApiDocument());

        Assert.True(File.Exists(fullPath));
    }

    private OpenApiDocument GetDocument() =>
        _factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger(DocumentName);

    private string GenerateOpenApiDocument() =>
        GetDocument().SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
}
