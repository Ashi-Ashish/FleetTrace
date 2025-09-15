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