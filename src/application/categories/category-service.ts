import {
  createCategory,
  normalizeCategoryName,
  type Category,
  type CreateCategoryValues,
} from "../../domain/categories/category.js";
import { CatalogError } from "../../domain/shared/catalog-error.js";
import type { CategoryRepository } from "../ports/category-repository.js";
import type { Clock, IdGenerator } from "../ports/runtime.js";

export class CategoryService {
  constructor(
    private readonly categories: CategoryRepository,
    private readonly ids: IdGenerator,
    private readonly clock: Clock,
  ) {}

  async create(values: CreateCategoryValues): Promise<Category> {
    const normalizedName = normalizeCategoryName(values.name);
    if (await this.categories.existsByNormalizedName(normalizedName)) {
      throw new CatalogError(
        "CATEGORY_NAME_ALREADY_EXISTS",
        "A category with this normalized name already exists.",
        { normalizedName },
      );
    }

    const category = createCategory(values, {
      id: this.ids.next("CATEGORY"),
      timestamp: this.clock.now(),
    });
    await this.categories.create(category);
    return category;
  }

  async list(): Promise<Category[]> {
    return this.categories.list();
  }
}
