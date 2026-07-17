namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoStateMetadataServiceTests
{
    [Fact]
    public async Task GetStateMetadata()
    {
        // Arrange
        var service = new ColoradoStateMetadataService();
        
        // Act
        var metadata = await service.GetStateMetadata();
        
        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("Colorado", metadata.Name);
    }
}