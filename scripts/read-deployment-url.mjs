import { readFile } from "node:fs/promises";

const [outputPath] = process.argv.slice(2);

if (!outputPath) {
  throw new Error("A Wrangler output file path is required.");
}

const records = (await readFile(outputPath, "utf8"))
  .split(/\r?\n/)
  .filter(Boolean)
  .map((line) => JSON.parse(line));

const deployment = records.findLast((record) => record.type === "deploy");
const target = deployment?.targets?.find((value) => {
  try {
    return new URL(value).protocol === "https:";
  } catch {
    return false;
  }
});

if (!target) {
  throw new Error("Wrangler did not report an HTTPS deployment target.");
}

const catalogBaseUrl = target.replace(/\/$/, "");
process.stdout.write(`catalog_base_url=${catalogBaseUrl}\n`);
