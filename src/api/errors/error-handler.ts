import type { ErrorHandler } from "hono";
import { HTTPException } from "hono/http-exception";
import type { ContentfulStatusCode } from "hono/utils/http-status";

import { CatalogError, type CatalogErrorCode } from "../../domain/shared/catalog-error.js";
import type { CatalogEnvironment } from "../types.js";
import { RequestValidationError } from "./request-validation-error.js";

const conflictCodes = new Set<CatalogErrorCode>([
  "PRODUCT_SKU_ALREADY_EXISTS",
  "CATEGORY_NAME_ALREADY_EXISTS",
]);

const notFoundCodes = new Set<CatalogErrorCode>(["PRODUCT_NOT_FOUND", "CATEGORY_NOT_FOUND"]);

function catalogStatus(code: CatalogErrorCode): ContentfulStatusCode {
  if (conflictCodes.has(code)) return 409;
  if (notFoundCodes.has(code)) return 404;
  return 400;
}

function httpExceptionStatus(error: HTTPException): ContentfulStatusCode {
  if ([400, 404, 409].includes(error.status)) return error.status;
  return 500;
}

export const errorHandler: ErrorHandler<CatalogEnvironment> = (error, context) => {
  const correlationId = context.get("correlationId") || crypto.randomUUID();
  let code = "INTERNAL_SERVER_ERROR";
  let message = "An unexpected error occurred.";
  let details: Readonly<Record<string, unknown>> = {};
  let status: ContentfulStatusCode = 500;

  if (error instanceof CatalogError) {
    code = error.code;
    message = error.message;
    details = error.details;
    status = catalogStatus(error.code);
  } else if (error instanceof RequestValidationError) {
    code = error.code;
    message = error.message;
    details = error.details;
    status = 400;
  } else if (error instanceof HTTPException) {
    code = error.status === 404 ? "ROUTE_NOT_FOUND" : "REQUEST_FAILED";
    message = error.message;
    status = httpExceptionStatus(error);
  }

  console.error(
    JSON.stringify({
      message: "catalog.request.failed",
      correlationId,
      code,
      status,
      path: new URL(context.req.url).pathname,
      error: error instanceof Error ? error.message : String(error),
    }),
  );

  return context.json({ error: { code, message, details }, correlationId }, status);
};
