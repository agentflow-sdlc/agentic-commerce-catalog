namespace Catalog.Managers.Categories;

public sealed class CategoryRequestException : Exception
{
    public CategoryRequestException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class CategoryConflictException : Exception
{
    public CategoryConflictException(string normalizedName, Exception? innerException = null)
        : base("A category with this normalized name already exists.", innerException)
    {
        NormalizedName = normalizedName;
    }

    public string NormalizedName { get; }
}

public sealed class CategoryNotFoundException : Exception
{
    public CategoryNotFoundException(string categoryId, Exception? innerException = null)
        : base("The requested category does not exist.", innerException)
    {
        CategoryId = categoryId;
    }

    public string CategoryId { get; }
}
