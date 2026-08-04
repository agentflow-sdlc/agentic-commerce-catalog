import { describe, expect, it } from "vitest";

import { createCategory, normalizeCategoryName } from "../../src/domain/categories/category.js";

const metadata = {
  id: "CATEGORY-00000000-0000-4000-8000-000000000001",
  timestamp: "2026-08-04T00:00:00.000Z",
};

describe("Category domain", () => {
  it("creates a valid category", () => {
    expect(createCategory({ name: "Electronics" }, metadata)).toMatchObject({
      id: metadata.id,
      name: "Electronics",
      normalizedName: "electronics",
    });
  });

  it("requires a name", () => {
    expect(() => createCategory({ name: "  " }, metadata)).toThrow(
      expect.objectContaining({ code: "CATEGORY_NAME_REQUIRED" }),
    );
  });

  it("normalizes case and repeated whitespace for duplicate prevention", () => {
    expect(normalizeCategoryName("  Home   Appliances ")).toBe("home appliances");
    expect(normalizeCategoryName("HOME APPLIANCES")).toBe("home appliances");
  });
});
