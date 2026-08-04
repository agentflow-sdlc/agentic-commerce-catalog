import { CatalogError } from "../shared/catalog-error.js";

export interface Product {
  id: string;
  sku: string;
  name: string;
  description?: string;
  price: number;
  categoryId?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProductValues {
  sku: string;
  name: string;
  description?: string;
  price: number;
  categoryId?: string;
}

export interface NewEntityMetadata {
  id: string;
  timestamp: string;
}

export function normalizeSku(value: string): string {
  const sku = value.trim().toUpperCase();
  if (sku.length === 0) {
    throw new CatalogError("PRODUCT_SKU_REQUIRED", "Product SKU is required.");
  }
  return sku;
}

function normalizeProductName(value: string): string {
  const name = value.trim();
  if (name.length === 0) {
    throw new CatalogError("PRODUCT_NAME_REQUIRED", "Product name is required.");
  }
  return name;
}

function validatePrice(value: number): number {
  if (!Number.isFinite(value) || value < 0) {
    throw new CatalogError(
      "PRODUCT_PRICE_INVALID",
      "Product price must be a finite number greater than or equal to zero.",
    );
  }
  return value;
}

function optionalTrimmed(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : undefined;
}

export function createProduct(values: CreateProductValues, metadata: NewEntityMetadata): Product {
  const description = optionalTrimmed(values.description);
  const categoryId = optionalTrimmed(values.categoryId);

  return {
    id: metadata.id,
    sku: normalizeSku(values.sku),
    name: normalizeProductName(values.name),
    price: validatePrice(values.price),
    isActive: true,
    createdAt: metadata.timestamp,
    updatedAt: metadata.timestamp,
    ...(description ? { description } : {}),
    ...(categoryId ? { categoryId } : {}),
  };
}

export function changeProductStatus(
  product: Product,
  isActive: boolean,
  updatedAt: string,
): Product {
  return { ...product, isActive, updatedAt };
}
