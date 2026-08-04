import { CategoryService } from "../application/categories/category-service.js";
import { ProductService } from "../application/products/product-service.js";
import { SystemClock, WebCryptoIdGenerator } from "../infrastructure/cloudflare/runtime.js";
import { D1CategoryRepository } from "../infrastructure/cloudflare/repositories/d1-category-repository.js";
import { D1ProductRepository } from "../infrastructure/cloudflare/repositories/d1-product-repository.js";

export function createCatalogServices(database: D1Database) {
  const categories = new D1CategoryRepository(database);
  const products = new D1ProductRepository(database);
  const ids = new WebCryptoIdGenerator();
  const clock = new SystemClock();

  return {
    categories: new CategoryService(categories, ids, clock),
    products: new ProductService(products, categories, ids, clock),
  };
}
