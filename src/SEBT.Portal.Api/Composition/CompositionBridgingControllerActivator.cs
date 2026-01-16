using System.Composition;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace SEBT.Portal.Api.Composition;

public class CompositionBridgingControllerActivator(IControllerActivator inner) : IControllerActivator
{
    public object Create(ControllerContext context)
    {
        var controller = inner.Create(context);

        var compositionContext = context.HttpContext.RequestServices.GetRequiredService<CompositionContext>();
        compositionContext.SatisfyImports(controller);

        return controller;
    }

    public void Release(ControllerContext context, object controller) =>
        inner.Release(context, controller);

    public ValueTask ReleaseAsync(ControllerContext context, object controller) =>
        inner.ReleaseAsync(context, controller);
}
