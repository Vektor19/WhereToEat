/**
 * Barrel for the `core` analytics cross-cutting layer: the session-id provider supplying the
 * per-session opaque client correlation id, the event builders (Step 8) that stamp it onto every
 * `RawEventBody`, and the batching emitter (Step 16) that is the single path flushing those events to
 * the ingest endpoint. Re-exported through the library public-api so the `app` consumes it through the
 * one boundary.
 */
export * from './session-id.provider';
export * from './event-builders';
export * from './analytics-emitter.service';
