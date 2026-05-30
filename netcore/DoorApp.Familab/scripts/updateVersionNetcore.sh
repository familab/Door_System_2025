#!/usr/bin/env bash
# updateVersionNetcore.sh
# Updates netcore/DoorApp.Familab/src/AssemblyInfo.cs with version information.
#
# Version source (in priority order):
#   1. First CLI argument:    ./updateVersionNetcore.sh 1.2.3-beta
#   2. A repo-root version-*.txt file (produced by the GitVersion CI step)
#   3. Fallback "0.0.1-dev"
#
# AssemblyVersion / AssemblyFileVersion must be numeric MAJOR.MINOR.PATCH.0;
# AssemblyInformationalVersion carries the full semantic version.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSEMBLY_INFO="${SCRIPT_DIR}/../src/AssemblyInfo.cs"

VERSION="${1:-}"
MAJOR_MINOR_PATCH=""

if [[ -z "${VERSION}" ]]; then
  shopt -s nullglob
  version_files=( version-*.txt )
  if (( ${#version_files[@]} > 0 )); then
    vf="${version_files[0]}"
    echo "Reading version from ${vf}"
    VERSION="$(grep '^version=' "${vf}" | head -n1 | cut -d'=' -f2- || true)"
    MAJOR_MINOR_PATCH="$(grep '^major_minor_patch=' "${vf}" | head -n1 | cut -d'=' -f2- || true)"
  fi
fi

if [[ -z "${VERSION}" ]]; then
  VERSION="0.0.1-dev"
fi

if [[ -z "${MAJOR_MINOR_PATCH}" ]]; then
  # Strip any pre-release/build suffix to obtain MAJOR.MINOR.PATCH
  MAJOR_MINOR_PATCH="$(echo "${VERSION}" | sed -E 's/[-+].*$//')"
fi

# Ensure MAJOR_MINOR_PATCH has three numeric components
if ! [[ "${MAJOR_MINOR_PATCH}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  MAJOR_MINOR_PATCH="0.0.1"
fi

echo "Informational version: ${VERSION}"
echo "Assembly version:      ${MAJOR_MINOR_PATCH}.0"
echo "Updating ${ASSEMBLY_INFO}"

cat > "${ASSEMBLY_INFO}" <<EOF
// -----------------------------------------------------------------------------
// AssemblyInfo.cs
//
// This file holds the embedded version of the application. It is updated
// automatically by the CI pipeline (see .github/workflows/deploynetcore.yml).
//
// The values here are read at runtime by AssemblyVersionProvider.
// -----------------------------------------------------------------------------
using System.Reflection;

[assembly: AssemblyVersion("${MAJOR_MINOR_PATCH}.0")]
[assembly: AssemblyFileVersion("${MAJOR_MINOR_PATCH}.0")]
[assembly: AssemblyInformationalVersion("${VERSION}")]
EOF

echo "AssemblyInfo.cs updated to ${VERSION}"
