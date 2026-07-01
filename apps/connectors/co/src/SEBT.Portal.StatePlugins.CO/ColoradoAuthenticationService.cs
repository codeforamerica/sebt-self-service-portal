using System.Composition;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.StatesPlugins.Interfaces;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoAuthenticationService : IStateAuthenticationService
{
    /// <summary>                                                                                                                                 
    /// Configures Swagger to display JWT Bearer authentication options in the UI.                                                                
    /// </summary>                                                                                                                                
    /// <param name="options">The Swagger generation options to configure.</param>
    public void ConfigureSwaggerGenSecurityOptions(SwaggerGenOptions options)
    {
        // Add JWT Bearer authentication to Swagger
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}