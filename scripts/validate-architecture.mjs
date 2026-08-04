import { readdir, readFile } from "node:fs/promises";
import { extname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = fileURLToPath(new URL("../", import.meta.url));

const boundaries = [
  {
    name: "domain",
    directory: join(repositoryRoot, "src", "domain"),
    forbidden: [
      { pattern: /(?:^|\/)hono(?:\/|$)/i, reason: "Hono" },
      { pattern: /cloudflare/i, reason: "Cloudflare" },
      { pattern: /(?:^|[./-])d1(?:[./-]|$)/i, reason: "D1" },
    ],
  },
  {
    name: "application",
    directory: join(repositoryRoot, "src", "application"),
    forbidden: [
      { pattern: /(?:^|\/)hono(?:\/|$)/i, reason: "Hono" },
      { pattern: /(?:^|\/)infrastructure(?:\/|$)/i, reason: "infrastructure" },
      { pattern: /cloudflare/i, reason: "Cloudflare" },
      { pattern: /(?:^|[./-])d1(?:[./-]|$)/i, reason: "D1" },
    ],
  },
];

async function sourceFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(
    entries.map((entry) => {
      const path = join(directory, entry.name);
      return entry.isDirectory() ? sourceFiles(path) : Promise.resolve([path]);
    }),
  );
  return nested.flat().filter((path) => extname(path) === ".ts");
}

const importPattern = /(?:from\s+|import\s*)["']([^"']+)["']/g;
const violations = [];

for (const boundary of boundaries) {
  for (const path of await sourceFiles(boundary.directory)) {
    const source = await readFile(path, "utf8");
    for (const match of source.matchAll(importPattern)) {
      const specifier = match[1];
      for (const forbidden of boundary.forbidden) {
        if (forbidden.pattern.test(specifier)) {
          violations.push(
            `${relative(repositoryRoot, path)} imports ${specifier} (${forbidden.reason} is forbidden in ${boundary.name})`,
          );
        }
      }
    }
  }
}

if (violations.length > 0) {
  console.error(
    "Architecture validation failed:\n" + violations.map((item) => `- ${item}`).join("\n"),
  );
  process.exitCode = 1;
} else {
  console.log("Architecture validation passed for domain and application boundaries.");
}
