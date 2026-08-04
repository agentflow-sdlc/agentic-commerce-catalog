import eslint from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";

const typedConfigs = [
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
].map((config) => ({ ...config, files: ["**/*.ts"] }));

export default tseslint.config(
  {
    ignores: [
      ".wrangler/**",
      "coverage/**",
      "dist/**",
      "node_modules/**",
      "worker-configuration.d.ts",
    ],
  },
  { ...eslint.configs.recommended, files: ["**/*.{js,mjs,ts}"] },
  {
    files: ["**/*.{js,mjs}"],
    languageOptions: { globals: globals.node },
  },
  ...typedConfigs,
  {
    files: ["**/*.ts"],
    languageOptions: {
      globals: { ...globals.node, ...globals.worker },
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      "@typescript-eslint/consistent-type-imports": "error",
      "@typescript-eslint/no-floating-promises": "error",
      "@typescript-eslint/no-misused-promises": "error",
    },
  },
);
