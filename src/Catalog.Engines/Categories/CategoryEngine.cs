namespace Catalog.Engines.Categories;

public interface ICategoryEngine
{
    Category Create(
        string id,
        string? name,
        string? description,
        DateTimeOffset timestamp);
}

public sealed class CategoryEngine : ICategoryEngine
{
    public const string IdPrefix = "CATEGORY-";
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;

    public Category Create(
        string id,
        string? name,
        string? description,
        DateTimeOffset timestamp)
    {
        var normalizedId = NormalizeId(id);
        var displayName = NormalizeDisplayName(name);
        var normalizedName = displayName.ToLowerInvariant();
        var normalizedDescription = NormalizeDescription(description);
        var utcTimestamp = timestamp.ToUniversalTime();

        return new Category(
            normalizedId,
            displayName,
            normalizedName,
            normalizedDescription,
            utcTimestamp,
            utcTimestamp);
    }

    private static string NormalizeId(string? id)
    {
        var normalized = id?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || !normalized.StartsWith(IdPrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(normalized[IdPrefix.Length..], "D", out var identifier))
        {
            throw new CategoryValidationException(
                "CATEGORY_ID_INVALID",
                "Category ID must use the CATEGORY-<guid> format.");
        }

        return $"{IdPrefix}{identifier:D}";
    }

    private static string NormalizeDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new CategoryValidationException(
                "CATEGORY_NAME_REQUIRED",
                "Category name is required.");
        }

        var displayName = string.Join(
            " ",
            name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (displayName.Length > MaxNameLength)
        {
            throw new CategoryValidationException(
                "CATEGORY_NAME_TOO_LONG",
                $"Category name cannot exceed {MaxNameLength} characters.");
        }

        return displayName;
    }

    private static string? NormalizeDescription(string? description)
    {
        var normalized = description?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > MaxDescriptionLength)
        {
            throw new CategoryValidationException(
                "CATEGORY_DESCRIPTION_TOO_LONG",
                $"Category description cannot exceed {MaxDescriptionLength} characters.");
        }

        return normalized;
    }
}
