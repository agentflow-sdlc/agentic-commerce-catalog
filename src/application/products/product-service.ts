import type { CategoryRepository } from "../ports/category-repository.js";
import type { Clock, IdGenerator } from "../ports/runtime.js";
import type { ProductRepository } from "../ports/product-repository.js";
import {
  changeProductStatus,
  createProduct,
  normalizeSku,
  type CreateProductValues,
  type Product,
} from "../../domain/products/product.js";
import { CatalogError } from "../../domain/shared/catalog-error.js";

export class ProductService {
  constructor(
    private readonly products: ProductRepository,
    private readonly categories: CategoryRepository,
    private readonly ids: IdGenerator,
    private readonly clock: Clock,
  ) {}

  async create(values: CreateProductValues): Promise<Product> {
    const sku = normalizeSku(values.sku);
    if (await this.products.existsBySku(sku)) {
      throw new CatalogError(
        "PRODUCT_SKU_ALREADY_EXISTS",
        "A product with this SKU already exists.",
        { sku },
      );
    }

    if (values.categoryId && !(await this.categories.existsById(values.categoryId))) {
      throw new CatalogError("CATEGORY_NOT_FOUND", "The requested category does not exist.", {
        categoryId: values.categoryId,
      });
    }

    const product = createProduct(
      { ...values, sku },
      { id: this.ids.next("PRODUCT"), timestamp: this.clock.now() },
    );
    await this.products.create(product);
    return product;
  }

  async getById(id: string): Promise<Product> {
    const product = await this.products.findById(id);
    if (!product) {
      throw new CatalogError("PRODUCT_NOT_FOUND", "The requested product does not exist.", {
        productId: id,
      });
    }
    return product;
  }

  async list(): Promise<Product[]> {
    return this.products.list();
  }

  async setStatus(id: string, isActive: boolean): Promise<Product> {
    const existing = await this.getById(id);
    const updated = changeProductStatus(existing, isActive, this.clock.now());
    const changed = await this.products.updateStatus(id, updated.isActive, updated.updatedAt);
    if (!changed) {
      throw new CatalogError("PRODUCT_NOT_FOUND", "The requested product does not exist.", {
        productId: id,
      });
    }
    return updated;
  }
}
