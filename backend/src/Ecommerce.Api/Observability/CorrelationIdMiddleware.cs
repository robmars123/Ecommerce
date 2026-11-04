using Serilog.Context;

namespace Ecommerce.Api.Observability;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[CorrelationId.HeaderName].FirstOrDefault();
        var id = CorrelationId.IsValid(inbound) ? inbound! : CorrelationId.NewId();

        context.Items[CorrelationId.ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationId.LogProperty, id))
        {
            await next(context);
        }
    }
}
