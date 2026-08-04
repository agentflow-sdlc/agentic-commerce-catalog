import type { Category } from "../../domain/categories/category.js";

export interface CategoryRepository {
  create(category: Category): Promise<void>;
  list(): Promise<Category[]>;
  existsById(id: string): Promise<boolean>;
  existsByNormalizedName(normalizedName: string): Promise<boolean>;
}
