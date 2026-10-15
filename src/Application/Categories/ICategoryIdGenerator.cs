namespace Catalog.Application.Categories;

public interface ICategoryIdGenerator
{
    string NewId();
}

public sealed class CategoryIdGenerator : ICategoryIdGenerator
{
    public string NewId() => $"CATEGORY-{Guid.NewGuid()}";
}
