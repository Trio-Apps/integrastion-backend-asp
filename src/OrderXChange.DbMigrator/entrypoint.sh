#!/bin/sh
set -e

if [ "${RUN_DB_MIGRATOR:-true}" = "true" ]; then
  exec dotnet OrderXChange.DbMigrator.dll
fi

echo "DB migrator skipped (RUN_DB_MIGRATOR=false)"
