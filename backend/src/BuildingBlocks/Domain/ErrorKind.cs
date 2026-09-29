namespace Akiron.BuildingBlocks.Domain;

/// <summary>Decides the HTTP status an <see cref="Error"/> is reported with.</summary>
public enum ErrorKind
{
    /// <summary>400: the request is malformed.</summary>
    Validation,

    /// <summary>404: the resource does not exist for this tenant.</summary>
    NotFound,

    /// <summary>409: the request clashes with existing data.</summary>
    Conflict,

    /// <summary>422: well-formed, but a business rule refuses it.</summary>
    Rule,

    /// <summary>401: the caller is not authenticated.</summary>
    Unauthorized,

    /// <summary>403: the caller is authenticated but not allowed.</summary>
    Forbidden,
}
