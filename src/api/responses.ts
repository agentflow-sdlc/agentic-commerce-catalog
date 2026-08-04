import type { Context } from "hono";
import type { ContentfulStatusCode } from "hono/utils/http-status";

import type { Category } from "../domain/categories/category.js";
import type { CatalogEnvironment } from "./types.js";

export function success<T>(
  context: Context<CatalogEnvironment>,
  data: T,
  status: ContentfulStatusCode = 200,
) {
  return context.json({ data, correlationId: context.get("correlationId") }, status);
}

export function categoryResponse(category: Category) {
  return {
    id: category.id,
    name: category.name,
    createdAt: category.createdAt,
    updatedAt: category.updatedAt,
    ...(category.description ? { description: category.description } : {}),
  };
}
