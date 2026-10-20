#!/bin/sh
set -eu

# Opt-in initialization for a single Render instance. Existing seed markers preserve edits.
if [ "${Database__InitializeOnStart:-false}" = "true" ]; then
    dotnet MellowHouse.Web.dll --migrate --seed
fi

exec dotnet MellowHouse.Web.dll "$@"
