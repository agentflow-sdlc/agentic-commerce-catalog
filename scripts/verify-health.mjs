const [argumentUrl] = process.argv.slice(2);
const baseUrl = (argumentUrl ?? process.env.CATALOG_BASE_URL)?.replace(/\/$/, "");

if (!baseUrl) {
  throw new Error("CATALOG_BASE_URL or a URL argument is required.");
}

const healthUrl = new URL("/health", `${baseUrl}/`).toString();
const maximumAttempts = 5;

function isHealthy(payload) {
  return (
    payload !== null &&
    typeof payload === "object" &&
    payload.data?.status === "operational" &&
    payload.data?.service === "catalog-api" &&
    payload.data?.version === "0.1.0"
  );
}

for (let attempt = 1; attempt <= maximumAttempts; attempt += 1) {
  try {
    const response = await fetch(healthUrl, {
      headers: { "X-Correlation-ID": `deployment-health-${attempt}` },
      signal: AbortSignal.timeout(10_000),
    });
    const payload = await response.json();

    if (response.status === 200 && isHealthy(payload)) {
      console.log(`Catalog health check passed at ${healthUrl}.`);
      process.exit(0);
    }

    console.error(
      `Health check attempt ${attempt}/${maximumAttempts} returned an unexpected response.`,
    );
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`Health check attempt ${attempt}/${maximumAttempts} failed: ${message}`);
  }

  if (attempt < maximumAttempts) {
    await new Promise((resolve) => setTimeout(resolve, 5_000));
  }
}

throw new Error(`Catalog health check failed after ${maximumAttempts} attempts.`);
