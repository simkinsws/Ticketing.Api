namespace Ticketing.Api.Extensions;

public static class RequestIdHeaderMiddlewareExtensions
{
    private const string RequestIdHeader = "X-Request-ID";

    public static IApplicationBuilder UseRequestIdHeader(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var requestId = context.TraceIdentifier;
            
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(RequestIdHeader))
                {
                    context.Response.Headers.Append(RequestIdHeader, requestId);
                }
                return Task.CompletedTask;
            });

            await next();
        });
    }
}
