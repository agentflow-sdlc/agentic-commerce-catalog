export const catalogErrorCodes = [
  "PRODUCT_SKU_REQUIRED",
  "PRODUCT_NAME_REQUIRED",
  "PRODUCT_PRICE_INVALID",
  "PRODUCT_SKU_ALREADY_EXISTS",
  "PRODUCT_NOT_FOUND",
  "CATEGORY_NAME_REQUIRED",
  "CATEGORY_NAME_ALREADY_EXISTS",
  "CATEGORY_NOT_FOUND",
] as const;

export type CatalogErrorCode = (typeof catalogErrorCodes)[number];

export class CatalogError extends Error {
  constructor(
    readonly code: CatalogErrorCode,
    message: string,
    readonly details: Readonly<Record<string, unknown>> = {},
  ) {
    super(message);
    this.name = "CatalogError";
  }
}
