namespace Catalog.Clients.Api.Contracts;

public sealed record HealthResponse(string Status, string Service, string Version);
