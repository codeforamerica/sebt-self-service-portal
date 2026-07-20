using SEBT.Portal.StatePlugins.CO;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ThrowHelperTests
{
    [Fact]
    public void CreateCoNotImplementedException()
    {
        var ex = ThrowHelper.CreateCoNotImplementedException();

        Assert.IsType<NotImplementedException>(ex);
        Assert.Contains("Colorado", ex.Message);
    }
}
