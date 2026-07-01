using Swashbuckle.AspNetCore.SwaggerGen;

namespace SEBT.Portal.StatesPlugins.Interfaces;

public interface IStateAuthenticationService : IStatePlugin
{
    void ConfigureSwaggerGenSecurityOptions(SwaggerGenOptions options);
}