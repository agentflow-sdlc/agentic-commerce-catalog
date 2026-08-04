export interface CatalogEnvironment {
  Bindings: Env;
  Variables: {
    correlationId: string;
  };
}
