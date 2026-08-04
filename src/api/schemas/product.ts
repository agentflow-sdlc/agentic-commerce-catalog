import { z } from "zod";

export const createProductRequestSchema = z
  .object({
    sku: z.string().trim().min(1).max(64),
    name: z.string().trim().min(1).max(200),
    description: z.string().trim().max(2000).optional(),
    price: z.number().nonnegative(),
    categoryId: z.string().trim().min(1).max(128).optional(),
  })
  .strict();

export const updateProductStatusRequestSchema = z
  .object({
    isActive: z.boolean(),
  })
  .strict();
