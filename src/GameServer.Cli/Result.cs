using System.Diagnostics.CodeAnalysis;

namespace GameServer.Cli;

public sealed class Result<TSuccess, TFailure>
{
    private readonly bool _isSuccess;
    private readonly TSuccess? _success;
    private readonly TFailure? _failure;

    private Result(bool isSuccess, TSuccess? success, TFailure? failure)
    {
        _isSuccess = isSuccess;
        _success = success;
        _failure = failure;
    }

    public static Result<TSuccess, TFailure> Success(TSuccess value) => new(true, value, default);
    public static Result<TSuccess, TFailure> Failure(TFailure value) => new(false, default, value);

    public static implicit operator Result<TSuccess, TFailure>(TSuccess value) => Success(value);
    public static implicit operator Result<TSuccess, TFailure>(TFailure value) => Failure(value);

    public bool TrySuccess([NotNullWhen(true)] out TSuccess? success, [NotNullWhen(false)] out TFailure? failure)
    {
        success = _success!;
        failure = _failure!;
        return _isSuccess;
    }
}
