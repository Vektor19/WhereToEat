using WhereToEat.SharedKernel.Results;

namespace WhereToEat.AdminApi.Endpoints;

/// <summary>
/// Maps the admin use-cases' <see cref="Result"/> onto HTTP outcomes so every admin endpoint reports
/// failures the same way: a <see cref="ErrorType.NotFound"/> → 404, a <see cref="ErrorType.Validation"/>
/// or <see cref="ErrorType.Conflict"/> → 400, anything else → 400 (admin CRUD has no 5xx user-input
/// path), and success → 204 No Content (the writes return nothing to echo).
/// </summary>
internal static class AdminResults
{
    public static IResult ToHttp(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return Microsoft.AspNetCore.Http.Results.NoContent();
        }

        var payload = new { error = result.Error.Code, message = result.Error.Message };
        return result.Error.Type == ErrorType.NotFound
            ? Microsoft.AspNetCore.Http.Results.NotFound(payload)
            : Microsoft.AspNetCore.Http.Results.BadRequest(payload);
    }
}
