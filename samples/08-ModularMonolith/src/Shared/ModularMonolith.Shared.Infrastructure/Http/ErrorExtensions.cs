using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Abstractions.Results;

namespace ModularMonolith.Shared.Infrastructure.Http;

public static class ErrorExtensions
{
    // Result hatasını ProblemDetails yanıtına çevirir: Validation→400, NotFound→404, Conflict→409.
    public static IResult ToProblem(this Error error) => Results.Problem(
        title: error.Code,
        detail: error.Message,
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        });
}
