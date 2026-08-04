import type { $ZodIssue } from "zod/v4/core";

export class RequestValidationError extends Error {
  readonly code = "REQUEST_VALIDATION_FAILED";
  readonly details: Readonly<Record<string, unknown>>;

  constructor(issues: readonly $ZodIssue[]) {
    super("The request payload is invalid.");
    this.name = "RequestValidationError";
    this.details = {
      issues: issues.map((issue) => ({
        code: issue.code,
        message: issue.message,
        path: issue.path.map(String),
      })),
    };
  }
}
