using AgriSustain.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Middlewares;

/// <summary>
/// Receives the API logger for centralized technical-failure reporting; expected business
/// failures are already Result values.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Logs an unexpected failure on the server and returns sanitized ProblemDetails with a trace
    /// ID; database queries, credentials and stack traces are never sent to the client.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException
            && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        bool domainFailure = exception is BusinessRuleException;
        if (!domainFailure)
        {
            logger.LogError(exception, "Three-UC request failed with trace {TraceId}",
                context.TraceIdentifier);
        }

        context.Response.StatusCode = domainFailure ? 409 : 500;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = domainFailure ? exception.Message : "Không thể xử lý. Vui lòng thử lại.",
            Extensions = { ["code"] = domainFailure ? "BUSINESS_CONFLICT" : "SERVER_ERROR",
                ["traceId"] = context.TraceIdentifier }
        }, cancellationToken);
        return true;
    }
}
