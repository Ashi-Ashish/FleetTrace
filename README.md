# FleetTrace

## Quick Start

1. Copy the example environment file and set a strong password (do not commit your .env):

```bash
cp .env.example .env
# Edit .env and set POSTGRES_PASSWORD to a strong value
```

2. Bring up the core infrastructure (Postgres + Kafka):

```bash
docker compose up -d
```

Notes:
- docker-compose.yml now reads credentials from environment variables. Docker Compose automatically loads variables from a .env file in the project root.
- It's not recommended to commit real secrets (like POSTGRES_PASSWORD) to GitHub. Use .env locally and secret managers or CI/CD secrets in production.

## Telematics Event Schema

A canonical JSON Schema defines the message contract for events published to the `telematics.events` topic.

- Location: `contracts/telematics_event.schema.json`
- Version: 1.0.0 (backward-compatible changes in 1.x may only add optional fields or broaden enums)
- Idempotency/ordering keys: `event_id`, `vehicle_id`, `ts` (epoch millis)
- Strictness: unknown top-level properties are rejected (additionalProperties: false)

Why we use it
- Producers (e.g., simulator) can auto-generate valid payloads and fail fast on invalid data.
- Consumers (e.g., StreamWorker) can validate and dead-letter bad messages, and upsert by `event_id` safely.
- Enables contract/property tests in CI using a single source of truth.

### Validate payloads with Docker (no local deps)

You can validate JSON files against the schema using `ajv-cli` via a throwaway Node container:

```bash
# Validate example payloads
docker run --rm -v "$PWD":/data node:20 npx -y ajv-cli \
  validate -s /data/contracts/telematics_event.schema.json \
  -d /data/examples/*.json

# Validate a single file
docker run --rm -v "$PWD":/data node:20 npx -y ajv-cli \
  validate -s /data/contracts/telematics_event.schema.json \
  -d /data/path/to/your_payload.json
```

Example files are provided:
- `examples/valid_telematics_event.json`
- `examples/invalid_telematics_event__missing_required.json`

### Producer guidance
- Always set `schema_version` to the schema version you targeted (e.g., `1.0.0`).
- Generate stable UUIDs for `event_id` and a stable vehicle identifier for `vehicle_id`.
- Use UTC epoch milliseconds in `ts`.
- Provide `position.lat` and `position.lon`; other fields are optional but recommended when available.

### Evolving the schema
- Backward-compatible changes in 1.x: add optional properties, extend enums, loosen minima/maxima where safe.
- Breaking changes: bump major and publish under a new `$id` path (e.g., `.../2-0-0`) and update `schema_version` expectations.


## Unit consistency

- We standardize on meters-per-second for speed inside the pipeline. The canonical field is `speed_mps` (m/s). If your API/UI needs kilometers-per-hour, convert at the edges (kph = mps * 3.6).
- If you previously used `speed_kph` in docs or prototypes, consider it deprecated in favor of `speed_mps`.

## Forward compatibility stance

- The schema is intentionally strict at the root: `additionalProperties: false`. Unknown top-level fields are rejected to prevent silent contract drift and to keep consumers predictable.
- If you later need looser forward-compatibility (accept unknown fields while validating the known required set), you can switch to JSON Schema 2020-12's `unevaluatedProperties: true` at the root. Keep `required` minimal to avoid false negatives.
- Nullability guidance: prefer omitting fields when a value is unknown rather than sending `null`. If you must send nulls from some sources, we can broaden specific fields to allow unions like `["boolean","null"]` or `["number","null"]` in a backward-compatible minor version.
