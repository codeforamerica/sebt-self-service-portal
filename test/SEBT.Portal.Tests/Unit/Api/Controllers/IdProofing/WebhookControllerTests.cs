using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SEBT.Portal.Api.Controllers.IdProofing;
using SEBT.Portal.Api.Models.IdProofing;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.IdProofing;

public class WebhookControllerTests
{
    private readonly SocureSettings socureSettings = new();
    private readonly ILogger<WebhookController> logger = Substitute.For<ILogger<WebhookController>>();
    private readonly ICommandHandler<ProcessWebhookCommand> handler =
        Substitute.For<ICommandHandler<ProcessWebhookCommand>>();

    private WebhookController CreateController()
    {
        handler.Handle(Arg.Any<ProcessWebhookCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return new WebhookController(socureSettings, logger)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task HandleWebhook_StripsNewlineAndCarriageReturnFromBoundaryLog()
    {
        // CodeQL cs/log-forging (CWE-117): user-controlled webhook fields must not
        // be logged with embedded newlines or attackers can forge log entries.
        var controller = CreateController();
        var payload = new WebhookPayload
        {
            EventId = "evt-123\nINJECTED",
            EventType = "evaluation_completed\rINJECTED",
            Data = new WebhookData
            {
                EvalId = "eval-1\nFAKE",
                Decision = "ACCEPT\nFAKE"
            }
        };

        await controller.HandleWebhook(payload, handler, CancellationToken.None);

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                !state.ToString()!.Contains('\n') &&
                !state.ToString()!.Contains('\r')),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleWebhook_PreservesFieldContentsAroundStrippedNewlines()
    {
        // Sanitization should remove control characters but keep the surrounding
        // characters so the log entry is still useful for forensics.
        var controller = CreateController();
        var payload = new WebhookPayload
        {
            EventId = "evt-123\nINJECTED",
            EventType = "evaluation_completed",
        };

        await controller.HandleWebhook(payload, handler, CancellationToken.None);

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("evt-123INJECTED")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
