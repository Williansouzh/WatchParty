using Microsoft.AspNetCore.SignalR;
using WatchParty.Domain.Common;

namespace WatchParty.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToProblem(this Error error) => Results.Problem(
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        },
        title: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    public static IResult ToHttp(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();

    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsSuccess ? onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value) : result.Error.ToProblem();

    /// <summary>For hub methods: failures become a <see cref="HubException"/> whose message reaches the client.</summary>
    public static T OrThrow<T>(this Result<T> result) =>
        result.IsSuccess ? result.Value : throw new HubException(result.Error.Message);

    public static void OrThrow(this Result result)
    {
        if (result.IsFailure) throw new HubException(result.Error.Message);
    }
}
