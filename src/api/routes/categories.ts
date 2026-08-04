import { zValidator } from "@hono/zod-validator";
import { Hono } from "hono";

import { RequestValidationError } from "../errors/request-validation-error.js";
import { categoryResponse, success } from "../responses.js";
import { createCategoryRequestSchema } from "../schemas/category.js";
import { createCatalogServices } from "../services.js";
import type { CatalogEnvironment } from "../types.js";

export const categoryRoutes = new Hono<CatalogEnvironment>()
  .post(
    "/",
    zValidator("json", createCategoryRequestSchema, (result) => {
      if (!result.success) throw new RequestValidationError(result.error.issues);
    }),
    async (context) => {
      const services = createCatalogServices(context.env.DB);
      const input = context.req.valid("json");
      const category = await services.categories.create({
        name: input.name,
        ...(input.description !== undefined ? { description: input.description } : {}),
      });
      return success(context, categoryResponse(category), 201);
    },
  )
  .get("/", async (context) => {
    const services = createCatalogServices(context.env.DB);
    const categories = await services.categories.list();
    return success(context, categories.map(categoryResponse));
  });
