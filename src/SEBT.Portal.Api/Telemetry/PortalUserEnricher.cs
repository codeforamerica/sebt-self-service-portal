using Microsoft.AspNetCore.Http;
using SEBT.Portal.Api.Filters;
using SEBT.Portal.Core.Utilities;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace SEBT.Portal.Api.Telemetry;

/// <summary>
/// Appends portal user identity properties to every log event during authenticated requests:
/// <list type="bullet">
///   <item><c>portal_user_id</c> — resolved and DB-verified GUID, set by <see cref="ResolveUserFilter"/></item>
///   <item><c>portal_user_email</c> — masked email from JWT claims</item>
///   <item><c>portal_user_phone</c> — masked phone from JWT claims</item>
/// </list>
/// Email and phone are available from the JWT after authentication; user ID only after
/// <see cref="ResolveUserFilter"/> has run. All three are absent on unauthenticated requests.
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
        var context = _contextAccessor.HttpContext;
        if (context == null)
        {
            return;
        }

        if (context.Items[ResolveUserFilter.UserIdKey] is Guid userId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("portal_user_id", userId));
        }

        var maskedEmail = PiiMasker.MaskEmail(context.User.GetUserEmail());
        if (maskedEmail != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("portal_user_email", maskedEmail));
        }

        var maskedPhone = PiiMasker.MaskPhone(context.User.GetUserPhone());
        if (maskedPhone != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("portal_user_phone", maskedPhone));
        }
    }
}

public static class PortalUserEnricherExtensions
{
    public static LoggerConfiguration WithPortalUserId(this LoggerEnrichmentConfiguration enrich) =>
        enrich.With<PortalUserEnricher>();
}
