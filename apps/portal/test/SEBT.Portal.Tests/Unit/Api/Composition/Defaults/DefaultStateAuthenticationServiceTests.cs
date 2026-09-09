using SEBT.Portal.Api.Composition.Defaults;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SEBT.Portal.Tests.Unit.Api.Composition.Defaults;

public class DefaultStateAuthenticationServiceTests
{
    [Fact]
    public void ConfigureSwaggerGenSecurityOptions_AddsNoSecuritySchemes()
    {
        var service = new DefaultStateAuthenticationService();
        var options = new SwaggerGenOptions();

        service.ConfigureSwaggerGenSecurityOptions(options);

        Assert.Empty(options.SwaggerGeneratorOptions.SecuritySchemes);
        Assert.Empty(options.SwaggerGeneratorOptions.SecurityRequirements);
    }
}
