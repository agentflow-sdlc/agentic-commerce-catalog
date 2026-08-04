import { Hono } from "hono";

import { errorHandler } from "./errors/error-handler.js";
import { correlationMiddleware } from "./middleware/correlation.js";
import { categoryRoutes } from "./routes/categories.js";
import { healthRoutes } from "./routes/health.js";
import { productRoutes } from "./routes/products.js";
import type { CatalogEnvironment } from "./types.js";

export const app = new Hono<CatalogEnvironment>();

app.use("*", correlationMiddleware);
app.route("/health", healthRoutes);
app.route("/products", productRoutes);
app.route("/categories", categoryRoutes);
app.notFound((context) =>
  context.json(
    {
      error: {
        code: "ROUTE_NOT_FOUND",
        message: "The requested route does not exist.",
        details: {},
      },
      correlationId: context.get("correlationId"),
    },
    404,
  ),
);
app.onError(errorHandler);
