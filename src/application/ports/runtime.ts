export type EntityPrefix = "PRODUCT" | "CATEGORY";

export interface IdGenerator {
  next(prefix: EntityPrefix): string;
}

export interface Clock {
  now(): string;
}
