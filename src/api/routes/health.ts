import { Hono } from "hono";

import { success } from "../responses.js";
import type { CatalogEnvironment } from "../types.js";

export const healthRoutes = new Hono<CatalogEnvironment>().get("/", (context) =>
  success(context, {
    status: "operational",
    service: "catalog-api",
    version: "0.1.0",
  }),
);
