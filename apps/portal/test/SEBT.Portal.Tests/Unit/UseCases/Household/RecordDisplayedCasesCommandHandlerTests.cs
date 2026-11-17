using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

public class RecordDisplayedCasesCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("e5555555-5555-4555-8555-555555555555");

    private readonly IUserCaseRepository _userCaseRepository = Substitute.For<IUserCaseRepository>();

    private RecordDisplayedCasesCommandHandler CreateHandler() =>
        new(_userCaseRepository, NullLogger<RecordDisplayedCasesCommandHandler>.Instance);

    private static RecordDisplayedCasesCommand CreateCommand(
        IReadOnlyList<SummerEbtCase> cases,
        Guid? userId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, "user@example.com") };
        if (userId is Guid id)
        {
            claims.Add(new Claim("sub", id.ToString()));
        }

        return new RecordDisplayedCasesCommand
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            Cases = cases
        };
    }

    [Fact]
    public async Task Handle_UpsertsCasesThatHaveACaseId()
    {
        var command = CreateCommand(
            [
                new() { SummerEBTCaseID = "SEBT-001", ApplicationId = "APP-9", ApplicationStudentId = "STU-4" },
                new() { SummerEBTCaseID = "SEBT-002" },
                new() { SummerEBTCaseID = "  " },
                new() { ChildFirstName = "No", ChildLastName = "Id" }
            ],
            UserId);

        var result = await CreateHandler().Handle(command);

        Assert.True(result.IsSuccess);
        await _userCaseRepository.Received(1).UpsertDisplayedCasesAsync(
            UserId,
            Arg.Is<IReadOnlyList<UserCaseIdentifiers>>(list =>
                list.SequenceEqual(new[]
                {
                    new UserCaseIdentifiers("SEBT-001", "APP-9", "STU-4"),
                    new UserCaseIdentifiers("SEBT-002", null, null)
                })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIdIsMissing_ReturnsUnauthorizedWithoutUpserting()
    {
        var command = CreateCommand([new() { SummerEBTCaseID = "SEBT-001" }]);

        var result = await CreateHandler().Handle(command);

        Assert.IsType<UnauthorizedResult>(result);
        await _userCaseRepository.DidNotReceiveWithAnyArgs().UpsertDisplayedCasesAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_WhenNoCaseHasACaseId_SucceedsWithoutUpserting()
    {
        var command = CreateCommand([new() { ChildFirstName = "No", ChildLastName = "Id" }], UserId);

        var result = await CreateHandler().Handle(command);

        Assert.True(result.IsSuccess);
        await _userCaseRepository.DidNotReceiveWithAnyArgs().UpsertDisplayedCasesAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_ReturnsDependencyFailed()
    {
        _userCaseRepository
            .UpsertDisplayedCasesAsync(default, default!, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("database unavailable")));
        var command = CreateCommand([new() { SummerEBTCaseID = "SEBT-001" }], UserId);

        var result = await CreateHandler().Handle(command);

        Assert.IsType<DependencyFailedResult>(result);
    }

    [Fact]
    public async Task Handle_WhenCancelled_PropagatesCancellation()
    {
        _userCaseRepository
            .UpsertDisplayedCasesAsync(default, default!, default)
            .ThrowsAsyncForAnyArgs(new OperationCanceledException());
        var command = CreateCommand([new() { SummerEBTCaseID = "SEBT-001" }], UserId);

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateHandler().Handle(command));
    }
}
