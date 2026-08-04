import type { ProductRepository } from "../../../application/ports/product-repository.js";
import type { Product } from "../../../domain/products/product.js";
import { CatalogError } from "../../../domain/shared/catalog-error.js";
import type { ProductRow } from "../d1/rows.js";

function toProduct(row: ProductRow): Product {
  return {
    id: row.id,
    sku: row.sku,
    name: row.name,
    price: row.price,
    isActive: row.is_active === 1,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
    ...(row.description ? { description: row.description } : {}),
    ...(row.category_id ? { categoryId: row.category_id } : {}),
  };
}

function isUniqueSkuViolation(error: unknown): boolean {
  return error instanceof Error && error.message.includes("UNIQUE constraint failed: products.sku");
}

function isCategoryForeignKeyViolation(error: unknown): boolean {
  return error instanceof Error && error.message.includes("FOREIGN KEY constraint failed");
}

export class D1ProductRepository implements ProductRepository {
  constructor(private readonly db: D1Database) {}

  async create(product: Product): Promise<void> {
    try {
      await this.db
        .prepare(
          `INSERT INTO products (
            id, sku, name, description, price, category_id, is_active, created_at, updated_at
          ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`,
        )
        .bind(
          product.id,
          product.sku,
          product.name,
          product.description ?? null,
          product.price,
          product.categoryId ?? null,
          product.isActive ? 1 : 0,
          product.createdAt,
          product.updatedAt,
        )
        .run();
    } catch (error) {
      if (isUniqueSkuViolation(error)) {
        throw new CatalogError(
          "PRODUCT_SKU_ALREADY_EXISTS",
          "A product with this SKU already exists.",
          { sku: product.sku },
        );
      }
      if (isCategoryForeignKeyViolation(error)) {
        throw new CatalogError("CATEGORY_NOT_FOUND", "The requested category does not exist.", {
          categoryId: product.categoryId,
        });
      }
      throw error;
    }
  }

  async findById(id: string): Promise<Product | null> {
    const row = await this.db
      .prepare(
        `SELECT id, sku, name, description, price, category_id, is_active, created_at, updated_at
         FROM products
         WHERE id = ?`,
      )
      .bind(id)
      .first<ProductRow>();
    return row ? toProduct(row) : null;
  }

  async list(): Promise<Product[]> {
    const result = await this.db
      .prepare(
        `SELECT id, sku, name, description, price, category_id, is_active, created_at, updated_at
         FROM products
         ORDER BY created_at DESC, id DESC`,
      )
      .all<ProductRow>();
    return result.results.map(toProduct);
  }

  async existsBySku(sku: string): Promise<boolean> {
    const row = await this.db
      .prepare("SELECT 1 AS found FROM products WHERE sku = ? LIMIT 1")
      .bind(sku)
      .first<{ found: number }>();
    return row !== null;
  }

  async updateStatus(id: string, isActive: boolean, updatedAt: string): Promise<boolean> {
    const result = await this.db
      .prepare("UPDATE products SET is_active = ?, updated_at = ? WHERE id = ?")
      .bind(isActive ? 1 : 0, updatedAt, id)
      .run();
    return result.meta.changes > 0;
  }
}
