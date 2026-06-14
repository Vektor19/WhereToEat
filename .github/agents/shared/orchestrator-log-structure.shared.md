# Logging & Persistent Memory

## Agents Log

All agents MUST append to `docs/plans/{task_name}/agents-log.md` for debugging purposes. Agents never read this file directly.

This log is **append-only** and used **only for debugging**. Because the orchestrators do not read this log, you MUST also include all relevant information in your response to the orchestrator.

### Appending method

Do **not** use the `edit` tool directly on `agents-log.md`. Instead, append the entry with a shell command — call `bash` explicitly.

Preferred (uses the shared helper script):

```bash
bash .github/agents/scripts/append-log.sh "<task_name>" "<agent_name>" "<status>" "<message>"
```

Raw fallback (if the script is unavailable):

```bash
mkdir -p "docs/plans/<task_name>" && \
printf -- '---\n- **time:** %s\n- **agent:** <agent_name>\n- **status:** <status>\n- **message:** <message>\n\n' \
  "$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "docs/plans/<task_name>/agents-log.md"
```

Append one entry after each unit of work. Keep `<message>` to a concise, single-line summary of what you did and the outcome.

## Persistent Memory

If there are any important details, discoveries, or decisions that should be remembered for future steps or phases, you may store them in:

`docs/plans/{task_name}/notes/{agent_name}.md`

Keep memory concise — store only key facts that may be needed later, not large dumps. You may use the `edit` tool for the notes file (unlike the append-only log).
