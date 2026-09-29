namespace Akiron.BuildingBlocks.Domain;

/// <summary>
/// An expected failure. <see cref="Code"/> is stable and machine-readable
/// (<c>module.resource.reason</c>); the client turns it into text. <see cref="Detail"/> is English
/// and meant for logs and bug reports, never for end users (ADR-0006).
/// </summary>
public sealed record Error(string Code, string Detail, ErrorKind Kind)
{
    private static readonly IReadOnlyDictionary<string, object?> NoParameters = new Dictionary<string, object?>();

    /// <summary>Values the client needs to render its message, e.g. <c>{ "maxLength": 200 }</c>.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = NoParameters;

    public static Error Validation(string code, string detail) => new(code, detail, ErrorKind.Validation);

    public static Error NotFound(string code, string detail) => new(code, detail, ErrorKind.NotFound);

    public static Error Conflict(string code, string detail) => new(code, detail, ErrorKind.Conflict);

    public static Error Rule(string code, string detail) => new(code, detail, ErrorKind.Rule);

    public static Error Unauthorized(string code, string detail) => new(code, detail, ErrorKind.Unauthorized);

    public static Error Forbidden(string code, string detail) => new(code, detail, ErrorKind.Forbidden);
}
