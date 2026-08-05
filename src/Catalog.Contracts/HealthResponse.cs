namespace Catalog.Contracts;

public sealed record HealthResponse(string Status, string Service, string Version);
