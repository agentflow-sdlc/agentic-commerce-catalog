import type { MiddlewareHandler } from "hono";

import type { CatalogEnvironment } from "../types.js";

const acceptedCorrelationId = /^[A-Za-z0-9._:-]{1,128}$/;

export const correlationMiddleware: MiddlewareHandler<CatalogEnvironment> = async (
  context,
  next,
) => {
  const supplied = context.req.header("X-Correlation-ID")?.trim();
  const correlationId =
    supplied && acceptedCorrelationId.test(supplied) ? supplied : crypto.randomUUID();
  const startedAt = Date.now();

  context.set("correlationId", correlationId);
  context.header("X-Correlation-ID", correlationId);
  await next();

  console.log(
    JSON.stringify({
      message: "catalog.request.completed",
      correlationId,
      method: context.req.method,
      path: new URL(context.req.url).pathname,
      status: context.res.status,
      durationMs: Date.now() - startedAt,
    }),
  );
};
