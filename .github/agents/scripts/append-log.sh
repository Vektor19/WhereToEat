#!/usr/bin/env bash
# Append a structured entry to a task's append-only agents log.
#
# Usage:
#   append-log.sh <task_name> <agent_name> <status> <message>
#
# Notes:
#   - Creates docs/plans/<task_name>/ and agents-log.md if missing.
#   - This log is for debugging only; agents never read it back.
set -euo pipefail

TASK_NAME="${1:?task_name required}"
AGENT_NAME="${2:?agent_name required}"
STATUS="${3:?status required}"
MESSAGE="${4:?message required}"

LOG_DIR="docs/plans/${TASK_NAME}"
LOG_FILE="${LOG_DIR}/agents-log.md"

mkdir -p "${LOG_DIR}"

if [ ! -f "${LOG_FILE}" ]; then
  printf '# Agents Log — %s\n\n_Append-only debug log. Agents never read this file._\n\n' \
    "${TASK_NAME}" > "${LOG_FILE}"
fi

TS="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"

{
  printf -- '---\n'
  printf -- '- **time:** %s\n' "${TS}"
  printf -- '- **agent:** %s\n' "${AGENT_NAME}"
  printf -- '- **status:** %s\n' "${STATUS}"
  printf -- '- **message:** %s\n\n' "${MESSAGE}"
} >> "${LOG_FILE}"

echo "Appended log entry for ${AGENT_NAME} (${STATUS}) to ${LOG_FILE}"
