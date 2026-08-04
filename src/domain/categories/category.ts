import { CatalogError } from "../shared/catalog-error.js";

export interface Category {
  id: string;
  name: string;
  normalizedName: string;
  description?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCategoryValues {
  name: string;
  description?: string;
}

export interface NewCategoryMetadata {
  id: string;
  timestamp: string;
}

export function normalizeCategoryName(value: string): string {
  const normalized = value.trim().replace(/\s+/g, " ").toLowerCase();
  if (normalized.length === 0) {
    throw new CatalogError("CATEGORY_NAME_REQUIRED", "Category name is required.");
  }
  return normalized;
}

export function createCategory(
  values: CreateCategoryValues,
  metadata: NewCategoryMetadata,
): Category {
  const name = values.name.trim().replace(/\s+/g, " ");
  const normalizedName = normalizeCategoryName(name);
  const description = values.description?.trim();

  return {
    id: metadata.id,
    name,
    normalizedName,
    createdAt: metadata.timestamp,
    updatedAt: metadata.timestamp,
    ...(description ? { description } : {}),
  };
}
