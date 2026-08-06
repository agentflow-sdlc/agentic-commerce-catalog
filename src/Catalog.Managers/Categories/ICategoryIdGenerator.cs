namespace Catalog.Managers.Categories;

public interface ICategoryIdGenerator
{
    string NewId();
}

public sealed class CategoryIdGenerator : ICategoryIdGenerator
{
    public string NewId() => $"CATEGORY-{Guid.NewGuid()}";
}
