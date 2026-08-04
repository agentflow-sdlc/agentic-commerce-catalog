import type { Product } from "../../domain/products/product.js";

export interface ProductRepository {
  create(product: Product): Promise<void>;
  findById(id: string): Promise<Product | null>;
  list(): Promise<Product[]>;
  existsBySku(sku: string): Promise<boolean>;
  updateStatus(id: string, isActive: boolean, updatedAt: string): Promise<boolean>;
}
