namespace Hearth.Web.Services;

public sealed record ExecutionResult(bool IsSuccess, string? ErrorMessage, bool IsBusinessError)
{
    public static ExecutionResult Success() =>
        new(true, null, false);

    public static ExecutionResult BusinessFailure(string message) =>
        new(false, message, true);

    public static ExecutionResult UnexpectedFailure(string message) =>
        new(false, message, false);
}

public sealed record ExecutionResult<T>(bool IsSuccess, T? Value, string? ErrorMessage, bool IsBusinessError)
{
    public static ExecutionResult<T> Success(T value) =>
        new(true, value, null, false);

    public static ExecutionResult<T> BusinessFailure(string message) =>
        new(false, default, message, true);

    public static ExecutionResult<T> UnexpectedFailure(string message) =>
        new(false, default, message, false);
}
