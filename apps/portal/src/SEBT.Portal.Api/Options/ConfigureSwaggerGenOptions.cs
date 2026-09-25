using System.Reflection;
using Microsoft.Extensions.Options;
using SEBT.Portal.StatesPlugins.Interfaces;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SEBT.Portal.Api.Options;

internal class ConfigureSwaggerGenOptions(IStateAuthenticationService stateAuthenticationService)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        Log.Information("Configuring SwaggerGenOptions using state-specific authentication service.");
        // Delegates configuration to the state-specific authentication plugin
        stateAuthenticationService.ConfigureSwaggerGenSecurityOptions(options);

        IncludeXmlDocComments(options);
    }

    /// <summary>
    /// Feeds the controllers' <c>///</c> comments into the OpenAPI document, so that endpoint
    /// summaries and <c>&lt;response&gt;</c> descriptions appear in both the Swagger UI and the
    /// documentation site's REST reference. <c>GenerateDocumentationFile</c> already produces the
    /// XML beside the assembly; without this call Swashbuckle never reads it and every operation
    /// is described by its method name alone.
    /// </summary>
    private static void IncludeXmlDocComments(SwaggerGenOptions options)
    {
        var xmlPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

        if (!File.Exists(xmlPath))
        {
            // Published layouts that trim the XML file still serve a usable document, so this is
            // a degraded-but-working case rather than a startup failure.
            Log.Warning("XML documentation file not found at {XmlPath}; OpenAPI operations will have no descriptions.", xmlPath);
            return;
        }

        options.IncludeXmlComments(xmlPath);
    }
}
