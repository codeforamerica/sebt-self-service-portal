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

    [Fact]
    public void CreateUser_WhenBogusProducesIal1_ShouldNotSetIdProofingCompletedAt()
    {
        var sawIal1 = false;
        for (var i = 0; i < 400; i++)
        {
            var user = UserFactory.CreateUser();
            if (user.IalLevel != UserIalLevel.IAL1)
            {
                continue;
            }

            sawIal1 = true;
            Assert.Null(user.IdProofingCompletedAt);
        }

        Assert.True(sawIal1, "Expected at least one IAL1 user so the Bogus IAL/completed-at rule can be exercised.");
    }
}
