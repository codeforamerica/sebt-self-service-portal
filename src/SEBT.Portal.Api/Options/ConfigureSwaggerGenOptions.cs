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
    }
}
