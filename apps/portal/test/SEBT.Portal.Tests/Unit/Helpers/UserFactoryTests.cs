using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.TestUtilities.Helpers;

namespace SEBT.Portal.Tests.Unit.Helpers;

public class UserFactoryTests
{
    [Fact]
    public void CreateUserWithStatus_IAL1_ShouldNotSetIdProofingCompletedAt()
    {
        var user = UserFactory.CreateUserWithStatus(UserIalLevel.IAL1);

        Assert.Equal(UserIalLevel.IAL1, user.IalLevel);
        Assert.Null(user.IdProofingCompletedAt);
    }
}
