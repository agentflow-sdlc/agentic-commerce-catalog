namespace Catalog.Contracts;

public sealed record ErrorResponse(ErrorDetail Error, string CorrelationId);

public sealed record ErrorDetail(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?> Details);
