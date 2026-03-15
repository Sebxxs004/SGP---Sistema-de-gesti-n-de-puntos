namespace Gibag.Shared.Models;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string ErrorCode { get; }
    public string ErrorMessage { get; }

    protected Result(bool isSuccess, string errorCode, string errorMessage)
    {
        if (isSuccess && errorCode != string.Empty)
            throw new InvalidOperationException("Successful result cannot have an error.");
        if (!isSuccess && errorCode == string.Empty)
            throw new InvalidOperationException("Failed result must have an error.");

        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new(true, string.Empty, string.Empty);
    public static Result Failure(string errorCode, string errorMessage) => new(false, errorCode, errorMessage);
    
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string errorCode, string errorMessage) => Result<T>.Failure(errorCode, errorMessage);
}

public class Result<T> : Result
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, string errorCode, string errorMessage)
        : base(isSuccess, errorCode, errorMessage)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result cannot be accessed.");

    public static Result<T> Success(T value) => new(value, true, string.Empty, string.Empty);
    public static new Result<T> Failure(string errorCode, string errorMessage) => new(default, false, errorCode, errorMessage);
}
