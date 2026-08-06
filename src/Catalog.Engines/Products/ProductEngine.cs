namespace Catalog.Engines.Products;

public interface IProductEngine
{
    string NormalizeId(string? id);

    Product Create(
        string id,
        string? sku,
        string? name,
        string? description,
        decimal? price,
        DateTimeOffset timestamp);
}

public sealed class ProductEngine : IProductEngine
{
    public const string IdPrefix = "PRODUCT-";
    public const int MaxSkuLength = 64;
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const decimal MaxPrice = 9999999999999999.99m;

    public string NormalizeId(string? id)
    {
        var normalized = id?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || !normalized.StartsWith(IdPrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(normalized[IdPrefix.Length..], "D", out var identifier))
        {
            throw new ProductValidationException(
                "PRODUCT_ID_INVALID",
                "Product ID must use the PRODUCT-<guid> format.");
        }

        return $"{IdPrefix}{identifier:D}";
    }

    public Product Create(
        string id,
        string? sku,
        string? name,
        string? description,
        decimal? price,
        DateTimeOffset timestamp)
    {
        var normalizedId = NormalizeId(id);
        var normalizedSku = RequireText(sku, "PRODUCT_SKU_REQUIRED", "SKU is required.")
            .ToUpperInvariant();
        var normalizedName = RequireText(name, "PRODUCT_NAME_REQUIRED", "Product name is required.");
        var normalizedDescription = NormalizeOptionalText(description);

        EnsureMaximumLength(
            normalizedSku,
            MaxSkuLength,
            "PRODUCT_SKU_TOO_LONG",
            $"SKU cannot exceed {MaxSkuLength} characters.");
        EnsureMaximumLength(
            normalizedName,
            MaxNameLength,
            "PRODUCT_NAME_TOO_LONG",
            $"Product name cannot exceed {MaxNameLength} characters.");

        if (normalizedDescription is not null)
        {
            EnsureMaximumLength(
                normalizedDescription,
                MaxDescriptionLength,
                "PRODUCT_DESCRIPTION_TOO_LONG",
                $"Product description cannot exceed {MaxDescriptionLength} characters.");
        }

        if (price is null)
        {
            throw new ProductValidationException(
                "PRODUCT_PRICE_REQUIRED",
                "Product price is required.");
        }

        if (price < 0)
        {
            throw new ProductValidationException(
                "PRODUCT_PRICE_INVALID",
                "Product price cannot be negative.");
        }

        if (price > MaxPrice)
        {
            throw new ProductValidationException(
                "PRODUCT_PRICE_OUT_OF_RANGE",
                "Product price exceeds the supported decimal(18,2) range.");
        }

        if (decimal.Round(price.Value, 2) != price.Value)
        {
            throw new ProductValidationException(
                "PRODUCT_PRICE_PRECISION_INVALID",
                "Product price cannot contain more than two decimal places.");
        }

        var utcTimestamp = timestamp.ToUniversalTime();
        return new Product(
            normalizedId,
            normalizedSku,
            normalizedName,
            normalizedDescription,
            price.Value,
            true,
            utcTimestamp,
            utcTimestamp);
    }

    private static string RequireText(string? value, string code, string message)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ProductValidationException(code, message)
            : normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void EnsureMaximumLength(
        string value,
        int maximumLength,
        string code,
        string message)
    {
        if (value.Length > maximumLength)
        {
            throw new ProductValidationException(code, message);
        }
    }
}
