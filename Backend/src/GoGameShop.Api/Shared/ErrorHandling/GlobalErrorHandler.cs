using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace GoGameShop.Api.Shared.ErrorHandling;

public class GlobalErrorHandler(ILogger<GlobalErrorHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var traceId = Activity.Current?.TraceId;
        logger.LogError(
            exception,
            "Could not process a request on machine {Machine}. TraceId {TraceId}",
            Environment.MachineName,
            traceId
        );

        await Results
            .Problem(
                title: "An error occurd while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: new Dictionary<string, object?> { { "traceId", traceId.ToString() } }
            )
            .ExecuteAsync(httpContext);

        return true;
    }
}
