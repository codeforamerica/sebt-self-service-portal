using Microsoft.AspNetCore.Http;
using SEBT.Portal.Core.Utilities;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace SEBT.Portal.Api.Telemetry;

/// <summary>
/// Appends portal user identity properties to every log event during authenticated requests:
/// <list type="bullet">
///   <item><c>portal_user_id</c> — portal user GUID from the JWT <c>sub</c> claim</item>
///   <item><c>portal_user_email</c> — masked email from JWT claims</item>
///   <item><c>portal_user_phone</c> — masked phone from JWT claims</item>
/// </list>
/// All three are read directly from JWT claims and are available as soon as authentication
/// succeeds. Absent on unauthenticated requests.
/// </summary>
public class PortalUserEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _contextAccessor;

    public PortalUserEnricher() : this(new HttpContextAccessor()) { }

    public PortalUserEnricher(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var user = _contextAccessor.HttpContext?.User;
        if (user == null)
        {
            return;
        }

        if (user.GetUserId() is Guid userId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("portal_user_id", userId));
        }

        var maskedEmail = PiiMasker.MaskEmail(user.GetUserEmail());
        if (maskedEmail != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("portal_user_email", maskedEmail));
        }

        var maskedPhone = PiiMasker.MaskPhone(user.GetUserPhone());
        if (maskedPhone != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("portal_user_phone", maskedPhone));
        }
    }
}

public static class PortalUserEnricherExtensions
{
    public static LoggerConfiguration WithPortalUserInfo(this LoggerEnrichmentConfiguration enrich) =>
        enrich.With<PortalUserEnricher>();
}
