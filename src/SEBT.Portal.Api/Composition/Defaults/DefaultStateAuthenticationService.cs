using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// Default implementation when no state-specific IStateAuthenticationService is provided.
/// Provides minimal Swagger configuration without state-specific auth.                                                                           
/// </summary>
internal class DefaultStateAuthenticationService : IStateAuthenticationService
{
    public void ConfigureSwaggerGenSecurityOptions(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        // No-op default implementation
    }
}
