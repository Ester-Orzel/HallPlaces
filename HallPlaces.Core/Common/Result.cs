namespace HallPlaces.Core.Common;

// What a Service returns instead of throwing exceptions for EXPECTED failures
// ("seat already taken", "event not found"). Exceptions stay for the unexpected.
public class Result
{
    public bool IsSuccess { get; }
    public ErrorType ErrorType { get; }
    public string? Error { get; }

    protected Result(bool isSuccess, ErrorType errorType, string? error)
    {
        IsSuccess = isSuccess;
        ErrorType = errorType;
        Error = error;
    }

    public static Result Success() => new(true, ErrorType.None, null);

    public static Result Fail(ErrorType errorType, string error) => new(false, errorType, error);
}

// Same idea, but a successful result also carries a value (the DTO to return).
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, ErrorType errorType, string? error)
        : base(isSuccess, errorType, error)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, ErrorType.None, null);

    public new static Result<T> Fail(ErrorType errorType, string error) => new(false, default, errorType, error);
}
