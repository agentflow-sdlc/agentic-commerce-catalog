import type { Clock, EntityPrefix, IdGenerator } from "../../application/ports/runtime.js";

export class WebCryptoIdGenerator implements IdGenerator {
  next(prefix: EntityPrefix): string {
    return `${prefix}-${crypto.randomUUID()}`;
  }
}

export class SystemClock implements Clock {
  now(): string {
    return new Date().toISOString();
  }
}
