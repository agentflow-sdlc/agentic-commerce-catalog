import type { CategoryRepository } from "../../../application/ports/category-repository.js";
import type { Category } from "../../../domain/categories/category.js";
import { CatalogError } from "../../../domain/shared/catalog-error.js";
import type { CategoryRow } from "../d1/rows.js";

function toCategory(row: CategoryRow): Category {
  return {
    id: row.id,
    name: row.name,
    normalizedName: row.normalized_name,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
    ...(row.description ? { description: row.description } : {}),
  };
}

function isUniqueNameViolation(error: unknown): boolean {
  return (
    error instanceof Error &&
    error.message.includes("UNIQUE constraint failed: categories.normalized_name")
  );
}

export class D1CategoryRepository implements CategoryRepository {
  constructor(private readonly db: D1Database) {}

  async create(category: Category): Promise<void> {
    try {
      await this.db
        .prepare(
          `INSERT INTO categories (
            id, name, normalized_name, description, created_at, updated_at
          ) VALUES (?, ?, ?, ?, ?, ?)`,
        )
        .bind(
          category.id,
          category.name,
          category.normalizedName,
          category.description ?? null,
          category.createdAt,
          category.updatedAt,
        )
        .run();
    } catch (error) {
      if (isUniqueNameViolation(error)) {
        throw new CatalogError(
          "CATEGORY_NAME_ALREADY_EXISTS",
          "A category with this normalized name already exists.",
          { normalizedName: category.normalizedName },
        );
      }
      throw error;
    }
  }

  async list(): Promise<Category[]> {
    const result = await this.db
      .prepare(
        `SELECT id, name, normalized_name, description, created_at, updated_at
         FROM categories
         ORDER BY normalized_name ASC, id ASC`,
      )
      .all<CategoryRow>();
    return result.results.map(toCategory);
  }

  async existsById(id: string): Promise<boolean> {
    const row = await this.db
      .prepare("SELECT 1 AS found FROM categories WHERE id = ? LIMIT 1")
      .bind(id)
      .first<{ found: number }>();
    return row !== null;
  }

  async existsByNormalizedName(normalizedName: string): Promise<boolean> {
    const row = await this.db
      .prepare("SELECT 1 AS found FROM categories WHERE normalized_name = ? LIMIT 1")
      .bind(normalizedName)
      .first<{ found: number }>();
    return row !== null;
  }
}
