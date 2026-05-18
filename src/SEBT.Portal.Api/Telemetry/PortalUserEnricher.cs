using Microsoft.AspNetCore.Http;
using SEBT.Portal.Api.Filters;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace SEBT.Portal.Api.Telemetry;

/// <summary>
/// Appends <c>portal_user_id</c> to every log event during authenticated requests.
/// Reads the resolved portal user ID stored in <see cref="HttpContext.Items"/> by
/// <see cref="ResolveUserFilter"/>. Produces no property for unauthenticated or pre-auth events.
/// </summary>
public class PortalUserEnricher : ILogEventEnricher
{
    private const string PropertyName = "portal_user_id";
    private readonly IHttpContextAccessor _contextAccessor;

    public PortalUserEnricher() : this(new HttpContextAccessor()) { }

    public PortalUserEnricher(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (_contextAccessor.HttpContext?.Items[ResolveUserFilter.UserIdKey] is Guid userId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, userId));
        }
    }
}

public static class PortalUserEnricherExtensions
{
    public static LoggerConfiguration WithPortalUserId(this LoggerEnrichmentConfiguration enrich) =>
        enrich.With<PortalUserEnricher>();
}
