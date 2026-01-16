using Microsoft.Extensions.Options;
using SEBT.Portal.StatesPlugins.Interfaces;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SEBT.Portal.Api.Options;

public class ConfigureSwaggerGenOptions(IStateAuthenticationService stateAuthenticationService)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        // Delegates configuration to the state-specific authentication plugin
        stateAuthenticationService.ConfigureSwaggerGenSecurityOptions(options);
    }
}
