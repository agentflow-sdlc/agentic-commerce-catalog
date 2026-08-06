namespace Catalog.Engines.Categories;

public sealed class CategoryValidationException : Exception
{
    public CategoryValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class CategoryNameAlreadyExistsException : Exception
{
    public CategoryNameAlreadyExistsException(
        string normalizedName,
        Exception? innerException = null)
        : base("A category with this normalized name already exists.", innerException)
    {
        NormalizedName = normalizedName;
    }

    public string NormalizedName { get; }
}
