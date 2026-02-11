#!/usr/bin/env bash
set -euo pipefail

INFRA_PROJ="src/AuthService.Infrastructure/AuthService.Infrastructure.csproj"
API_PROJ="src/AuthService.Api/AuthService.Api.csproj"
CTX="AppDbContext"

if [ $# -lt 1 ]; then
  echo "Missing migration name."
  echo "Usage: $0 <MigrationName>"
  exit 1
fi

NAME=$1

echo "Adding migration: $NAME ..."
DOTNET_ENVIRONMENT=Development \
  dotnet ef migrations add "$NAME" \
    --project "$INFRA_PROJ" \
    --startup-project "$API_PROJ" \
    --context "$CTX" \
    --output-dir Persistence/Migrations

echo "Applying migration: $NAME ..."
DOTNET_ENVIRONMENT=Development \
  dotnet ef database update \
    --project "$INFRA_PROJ" \
    --startup-project "$API_PROJ" \
    --context "$CTX"

echo "Migration '$NAME' created and applied successfully."

