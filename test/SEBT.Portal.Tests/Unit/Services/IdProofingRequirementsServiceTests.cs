using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Services;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// Tests for the legacy IdProofingRequirementsService.
/// NOTE: This service has been superseded by the unified IAL requirements system.
/// These tests are retained as placeholders until the service is deleted in a forthcoming task.
/// </summary>
public class IdProofingRequirementsServiceTests
{
    private static IdProofingRequirementsService CreateService()
    {
        var settings = new IdProofingRequirementsSettings();
        var snapshot = Substitute.For<IOptionsSnapshot<IdProofingRequirementsSettings>>();
        snapshot.Value.Returns(settings);
        return new(snapshot);
    }

    [Fact]
    public void GetPiiVisibility_Superseded_ThrowsNotImplementedException()
    {
        var service = CreateService();
        Assert.Throws<NotImplementedException>(() => service.GetPiiVisibility(UserIalLevel.IAL1plus));
    }
}
