#!/usr/bin/env bash
# Extract a single step's raw markdown section from an implementation plan.
#
# Usage:
#   extract-step.sh <path-to-implementation.md> "<step_id>"
# Example:
#   extract-step.sh docs/plans/menu-parser/implementation.md "Step 2"
#
# Prints the lines from "### <step_id>:" up to (but not including) the next
# "### " step heading or the next "## " section heading.
set -euo pipefail

FILE="${1:?path to implementation.md required}"
STEP="${2:?step_id required (e.g. \"Step 1\")}"

awk -v s="${STEP}" '
  $0 ~ ("^### " s ":") { f = 1; c = 0 }
  f && /^### / { c++; if (c > 1) exit }
  f && /^## /  { exit }
  f            { print }
' "${FILE}"
