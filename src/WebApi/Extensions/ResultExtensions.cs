using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace WebApi.Extensions;

public static class ResultExtensions
{
    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Result<TIn>, TOut> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result);

    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        result.Match(
            onSuccess: value => new OkObjectResult(value),
            onFailure: failure => ToProblemResult(failure));

    private static ObjectResult ToProblemResult(Result result)
    {
        var problemDetails = new ProblemDetails
        {
            Title = result.Error.Code,
            Detail = result.Error.Message,
            Status = GetStatusCode(result.Error.Type),
            Type = GetProblemType(result.Error.Type)
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    private static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Faulure => StatusCodes.Status401Unauthorized,
            ErrorType.Validation or ErrorType.Problem => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string GetProblemType(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation or ErrorType.Problem =>
                "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            ErrorType.NotFound =>
                "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            ErrorType.Conflict =>
                "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            ErrorType.Forbidden =>
                "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            ErrorType.Faulure =>
                "https://tools.ietf.org/html/rfc7235#section-3.1",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
}
