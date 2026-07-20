using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoAuthenticationServiceTests
{
    [Fact]
    public void ConfigureSwaggerGenSecurityOptions_AddsJwtDefinitionAndRequirement()
    {
        // Arrange
        var options = new SwaggerGenOptions();
        var service = new ColoradoAuthenticationService();

        // Act
        service.ConfigureSwaggerGenSecurityOptions(options);

        // Assert
        var securityScheme = Assert.Single(options.SwaggerGeneratorOptions.SecuritySchemes);
        Assert.Equal("Bearer", securityScheme.Key);
        
        var securityRequirement =  Assert.Single(options.SwaggerGeneratorOptions.SecurityRequirements);
        var securityRequirementKey = Assert.Single(securityRequirement.Keys);
        var openApiSecurityScheme = Assert.IsType<OpenApiSecurityScheme>(securityRequirementKey);
        Assert.Equal("Bearer", openApiSecurityScheme.Reference.Id);
    }
}