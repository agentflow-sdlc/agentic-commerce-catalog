import { zValidator } from "@hono/zod-validator";
import { Hono } from "hono";

import { createCatalogServices } from "../services.js";
import { success } from "../responses.js";
import {
  createProductRequestSchema,
  updateProductStatusRequestSchema,
} from "../schemas/product.js";
import type { CatalogEnvironment } from "../types.js";
import { RequestValidationError } from "../errors/request-validation-error.js";

export const productRoutes = new Hono<CatalogEnvironment>()
  .post(
    "/",
    zValidator("json", createProductRequestSchema, (result) => {
      if (!result.success) throw new RequestValidationError(result.error.issues);
    }),
    async (context) => {
      const services = createCatalogServices(context.env.DB);
      const input = context.req.valid("json");
      const product = await services.products.create({
        sku: input.sku,
        name: input.name,
        price: input.price,
        ...(input.description !== undefined ? { description: input.description } : {}),
        ...(input.categoryId !== undefined ? { categoryId: input.categoryId } : {}),
      });
      return success(context, product, 201);
    },
  )
  .get("/", async (context) => {
    const services = createCatalogServices(context.env.DB);
    return success(context, await services.products.list());
  })
  .get("/:id", async (context) => {
    const services = createCatalogServices(context.env.DB);
    return success(context, await services.products.getById(context.req.param("id")));
  })
  .patch(
    "/:id/status",
    zValidator("json", updateProductStatusRequestSchema, (result) => {
      if (!result.success) throw new RequestValidationError(result.error.issues);
    }),
    async (context) => {
      const services = createCatalogServices(context.env.DB);
      const product = await services.products.setStatus(
        context.req.param("id"),
        context.req.valid("json").isActive,
      );
      return success(context, product);
    },
  );
