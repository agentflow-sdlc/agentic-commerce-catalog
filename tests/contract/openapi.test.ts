import { describe, expect, it } from "vitest";
import { parse } from "yaml";
import { z } from "zod";

import contractSource from "../../openapi/catalog-api.yaml?raw";
import { app } from "../../src/api/app.js";

const operationSchema = z.looseObject({
  responses: z.record(z.string(), z.unknown()),
});

const contractSchema = z.object({
  paths: z.record(z.string(), z.record(z.string(), operationSchema)),
});

const httpMethods = new Set(["delete", "get", "head", "options", "patch", "post", "put"]);

function normalizedPath(path: string): string {
  const withoutTrailingSlash = path.length > 1 ? path.replace(/\/$/, "") : path;
  return withoutTrailingSlash.replace(/:([^/]+)/g, "{$1}");
}

describe("OpenAPI implementation conformance", () => {
  const contract = contractSchema.parse(parse(contractSource) as unknown);
  const documentedOperations = Object.entries(contract.paths).flatMap(([path, pathItem]) =>
    Object.keys(pathItem)
      .filter((method) => httpMethods.has(method))
      .map((method) => `${method.toUpperCase()} ${normalizedPath(path)}`),
  );
  const implementedOperations = app.routes
    .filter((route) => httpMethods.has(route.method.toLowerCase()))
    .map((route) => `${route.method.toUpperCase()} ${normalizedPath(route.path)}`);

  it("documents exactly the HTTP operations registered by Hono", () => {
    expect(new Set(documentedOperations)).toEqual(new Set(implementedOperations));
  });

  it("documents a successful response for every operation", () => {
    for (const pathItem of Object.values(contract.paths)) {
      for (const [method, operation] of Object.entries(pathItem)) {
        if (!httpMethods.has(method)) continue;
        expect(
          Object.keys(operation.responses).some((status) => /^2\d\d$/.test(status)),
          `${method.toUpperCase()} must document a successful response`,
        ).toBe(true);
      }
    }
  });
});
