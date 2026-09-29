using System.Diagnostics.CodeAnalysis;

namespace Akiron.BuildingBlocks.Domain;

/// <summary>
/// The outcome of a use case: a value, or an expected <see cref="Domain.Error"/>. Exceptions are
/// reserved for failures nobody planned for.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error) => Error = error;

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result failed with '{Error.Code}'; check IsSuccess before reading Value.");

    private static Result<T> Success(T value) => new(value);

    private static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
