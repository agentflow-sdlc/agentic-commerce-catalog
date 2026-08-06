namespace Catalog.Contracts;

public sealed record CreateCategoryRequest(string? Name, string? Description = null);

public sealed record CategoryResponse(
    string Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CategoryResponseEnvelope(CategoryResponse Data, string CorrelationId);

public sealed record CategoryListResponseEnvelope(
    IReadOnlyList<CategoryResponse> Data,
    string CorrelationId);
