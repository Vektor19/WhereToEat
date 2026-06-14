# Orchestration Conventions

Shared rules for the orchestration **skills** (`/orchestrator-plan`, `/orchestrator-implement`)
and the worker **subagents** (`architect`, `architect-reviewer`, `developer`,
`developer-reviewer`, `qa`, `reflect`).

Subagents run in an isolated context window, so each one is told to read this file before
acting. The orchestrator skills run in the main session and read it too.

## Task artifacts

Everything for one task lives under `docs/plans/{task_name}/`:

| File | Written by | Purpose |
| --- | --- | --- |
| `design.md` | architect | High-level design (source of truth) |
| `implementation.md` | architect | Numbered, checkable steps |
| `design-questions.md` | architect | Open questions for the user (transient) |
| `design-review.md` | architect-reviewer | Graded design review (deleted before handover) |
| `implementation-review.md` | architect-reviewer | Graded plan review (deleted before handover) |
| `implementation-design-issues.md` | architect | Design flaws found while planning |
| `agents-log.md` | all subagents | Append-only debug log (never read back) |
| `notes/{agent_name}.md` | each subagent | Concise persistent memory |

`{task_name}` is a short, lowercase, hyphenated slug (e.g., `menu-parser`, `geo-distance`).

## `design.md` outline

High level only — no exact file paths, no import-level detail, no step-by-step code:

```md
# Design — {task_name}

## Goals
## Non-Goals
## Overview
## Key Components & Flows
## Key Decisions & Trade-offs
## Risks & Open Questions
```

## `implementation.md` step format

The file MUST contain a `## Implementation Steps` section. Each step uses this exact heading
shape so steps can be located and verified:

```md
## Implementation Steps

### Step 1: <concise title>
- **status:** [ ] pending
- **description:** <what to do and why>
- **files to modify:** <paths / new files>
- **acceptance criteria:** <objective, checkable conditions>
- **testing requirements:** <unit / integration / manual checks>
```

Status lifecycle: `[ ] pending` → `[~] in-progress` → `[x] done` → `[!] blocked`.

## Subagent response format

The orchestrator only sees a subagent's **final message**. End every worker response with a
short prose summary followed by this YAML block:

```yaml
step_id: "Step N"        # or "design" / "plan" during the planning phase
status: completed | needs_revision | blocked
summary: one-line outcome
issues:                  # empty list if none
  - severity: high | medium | low
    description: actionable detail
notes: context for the orchestrator / next agent
```

Status meaning:
- `completed` — done / approved with no suggestions, or tests passed.
- `needs_revision` — issues, bugs, or suggestions found; needs developer action.
- `blocked` — cannot proceed (external dependency, environment issue, or user decision).

## Logging (append-only)

After each unit of work, append a log entry. Do **not** use the `Edit` tool on the log —
run the script with `Bash`:

```bash
bash .claude/orchestration/append-log.sh "<task_name>" "<agent_name>" "<status>" "<message>"
```

Agents never read `agents-log.md`; always put what matters in your response too.

## Persistent memory

Store durable, concise facts in `docs/plans/{task_name}/notes/{agent_name}.md` (the `Edit`
tool is fine here). Key facts only — no large dumps.

> Optional: a subagent can instead use Claude Code's native cross-task memory by adding
> `memory: project` to its frontmatter, which gives it `.claude/agent-memory/<name>/MEMORY.md`.
> This system uses the per-task `notes/` files by default so memory stays scoped to a task.
