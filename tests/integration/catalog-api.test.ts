import { exports } from "cloudflare:workers";
import { describe, expect, it } from "vitest";
import { z } from "zod";

const productEnvelope = z.object({
  data: z.object({
    id: z.string(),
    sku: z.string(),
    name: z.string(),
    description: z.string().optional(),
    price: z.number(),
    categoryId: z.string().optional(),
    isActive: z.boolean(),
    createdAt: z.string(),
    updatedAt: z.string(),
  }),
  correlationId: z.string(),
});

const categoryEnvelope = z.object({
  data: z.object({
    id: z.string(),
    name: z.string(),
    description: z.string().optional(),
    createdAt: z.string(),
    updatedAt: z.string(),
  }),
  correlationId: z.string(),
});

const errorEnvelope = z.object({
  error: z.object({
    code: z.string(),
    message: z.string(),
    details: z.record(z.string(), z.unknown()),
  }),
  correlationId: z.string(),
});

async function request(path: string, init?: RequestInit): Promise<Response> {
  return exports.default.fetch(`https://catalog.test${path}`, init);
}

async function post(path: string, body: unknown): Promise<Response> {
  return request(path, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Correlation-ID": "integration-test" },
    body: JSON.stringify(body),
  });
}

async function createProduct(overrides: Record<string, unknown> = {}) {
  const response = await post("/products", {
    sku: `SKU-${crypto.randomUUID()}`,
    name: "Test product",
    price: 10,
    ...overrides,
  });
  expect(response.status).toBe(201);
  return productEnvelope.parse(await response.json()).data;
}

async function createCategory(name = `Category ${crypto.randomUUID()}`) {
  const response = await post("/categories", { name });
  expect(response.status).toBe(201);
  return categoryEnvelope.parse(await response.json()).data;
}

describe("Catalog API", () => {
  it("reports health", async () => {
    const response = await request("/health", { headers: { "X-Correlation-ID": "health-test" } });
    expect(response.status).toBe(200);
    expect(await response.json()).toMatchObject({
      data: { status: "operational", service: "catalog-api", version: "0.1.0" },
      correlationId: "health-test",
    });
    expect(response.headers.get("X-Correlation-ID")).toBe("health-test");
  });

  it("creates and retrieves a product by ID", async () => {
    const created = await createProduct({ price: 0 });
    const response = await request(`/products/${created.id}`);
    expect(response.status).toBe(200);
    expect(productEnvelope.parse(await response.json()).data).toEqual(created);
  });

  it("lists products", async () => {
    const created = await createProduct();
    const response = await request("/products");
    expect(response.status).toBe(200);
    const body = z
      .object({ data: z.array(productEnvelope.shape.data) })
      .parse(await response.json());
    expect(body.data).toContainEqual(created);
  });

  it("returns conflict for a duplicate normalized SKU", async () => {
    await createProduct({ sku: " duplicate-sku " });
    const response = await post("/products", { sku: "DUPLICATE-SKU", name: "Duplicate", price: 1 });
    expect(response.status).toBe(409);
    expect(errorEnvelope.parse(await response.json()).error.code).toBe(
      "PRODUCT_SKU_ALREADY_EXISTS",
    );
  });

  it("rejects a negative price", async () => {
    const response = await post("/products", { sku: "NEGATIVE", name: "Invalid", price: -1 });
    expect(response.status).toBe(400);
    expect(errorEnvelope.parse(await response.json()).error.code).toBe("REQUEST_VALIDATION_FAILED");
  });

  it("deactivates and activates a product", async () => {
    const created = await createProduct();
    const deactivate = await request(`/products/${created.id}/status`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ isActive: false }),
    });
    expect(deactivate.status).toBe(200);
    expect(productEnvelope.parse(await deactivate.json()).data.isActive).toBe(false);

    const activate = await request(`/products/${created.id}/status`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ isActive: true }),
    });
    expect(activate.status).toBe(200);
    expect(productEnvelope.parse(await activate.json()).data.isActive).toBe(true);
  });

  it("creates and lists categories", async () => {
    const created = await createCategory("Books");
    const response = await request("/categories");
    expect(response.status).toBe(200);
    const body = z
      .object({ data: z.array(categoryEnvelope.shape.data) })
      .parse(await response.json());
    expect(body.data).toContainEqual(created);
  });

  it("prevents duplicate category names after normalization", async () => {
    await createCategory("Home   Appliances");
    const response = await post("/categories", { name: " home appliances " });
    expect(response.status).toBe(409);
    expect(errorEnvelope.parse(await response.json()).error.code).toBe(
      "CATEGORY_NAME_ALREADY_EXISTS",
    );
  });

  it("associates a product with an existing category", async () => {
    const category = await createCategory();
    const product = await createProduct({ categoryId: category.id });
    expect(product.categoryId).toBe(category.id);
  });

  it("rejects a nonexistent category", async () => {
    const response = await post("/products", {
      sku: "MISSING-CATEGORY",
      name: "Invalid category",
      price: 5,
      categoryId: "CATEGORY-00000000-0000-4000-8000-000000000099",
    });
    expect(response.status).toBe(404);
    expect(errorEnvelope.parse(await response.json()).error.code).toBe("CATEGORY_NOT_FOUND");
  });

  it("returns 404 for a nonexistent product", async () => {
    const response = await request("/products/PRODUCT-DOES-NOT-EXIST");
    expect(response.status).toBe(404);
    expect(errorEnvelope.parse(await response.json()).error.code).toBe("PRODUCT_NOT_FOUND");
  });
});
