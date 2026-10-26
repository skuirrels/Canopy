#!/usr/bin/env bash
set -euo pipefail
planner_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$planner_root"
configuration="${1:-Debug}"
npm --prefix client ci
npm --prefix client run build
dotnet build Planner.sln --configuration "$configuration"
run_args=(run --no-build --no-launch-profile --configuration "$configuration" --project server/Planner.Api)
if [[ -n "${PLANNER_API_URL:-}" ]]; then
  run_args+=(-- --urls "$PLANNER_API_URL")
fi
exec dotnet "${run_args[@]}"
