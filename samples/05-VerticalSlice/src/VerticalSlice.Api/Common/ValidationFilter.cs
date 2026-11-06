using FluentValidation;

namespace VerticalSlice.Api.Common;

// [Common] Endpoint filter: MediatR pipeline behavior'ının Minimal API karşılığı.
// İstek handler'a ulaşmadan önce ilgili IValidator<T> çalıştırılır.
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "İstek gövdesi boş.");

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!result.IsValid)
            return Results.ValidationProblem(result.ToDictionary());

        return await next(context);
    }
}
