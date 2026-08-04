import { describe, expect, it } from "vitest";

import {
  changeProductStatus,
  createProduct,
  type CreateProductValues,
} from "../../src/domain/products/product.js";

const metadata = {
  id: "PRODUCT-00000000-0000-4000-8000-000000000001",
  timestamp: "2026-08-04T00:00:00.000Z",
};

function validProduct(overrides: Partial<CreateProductValues> = {}) {
  return createProduct(
    {
      sku: "catalog-001",
      name: "Catalog product",
      price: 10,
      ...overrides,
    },
    metadata,
  );
}

describe("Product domain", () => {
  it("creates a valid active product with a normalized SKU", () => {
    const product = validProduct();
    expect(product).toMatchObject({
      id: metadata.id,
      sku: "CATALOG-001",
      name: "Catalog product",
      price: 10,
      isActive: true,
    });
  });

  it("requires a SKU", () => {
    expect(() => validProduct({ sku: "  " })).toThrow(
      expect.objectContaining({ code: "PRODUCT_SKU_REQUIRED" }),
    );
  });

  it("requires a name", () => {
    expect(() => validProduct({ name: "  " })).toThrow(
      expect.objectContaining({ code: "PRODUCT_NAME_REQUIRED" }),
    );
  });

  it("allows a zero price", () => {
    expect(validProduct({ price: 0 }).price).toBe(0);
  });

  it("rejects a negative price", () => {
    expect(() => validProduct({ price: -0.01 })).toThrow(
      expect.objectContaining({ code: "PRODUCT_PRICE_INVALID" }),
    );
  });

  it("deactivates and reactivates a product", () => {
    const product = validProduct();
    const inactive = changeProductStatus(product, false, "2026-08-04T01:00:00.000Z");
    const active = changeProductStatus(inactive, true, "2026-08-04T02:00:00.000Z");
    expect(inactive.isActive).toBe(false);
    expect(active.isActive).toBe(true);
  });
});
