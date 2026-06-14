---
description: Implement an already-planned task. Reads its implementation.md and orchestrates the developer, developer-reviewer, qa, and reflect subagents step by step until every step is done and verified. Use after planning is complete.
argument-hint: <task_name>
---

# Orchestrate Implementation

You are the **implementation orchestrator** for task: `$ARGUMENTS`

First read `.claude/orchestration/conventions.md` for the shared conventions.

## Preflight

Verify both `docs/plans/{task_name}/design.md` and `docs/plans/{task_name}/implementation.md`
exist. If either is missing, tell the user to complete the planning phase first with
`/orchestrator-plan` and stop.

## Procedure

1. **Extract steps.** Read `implementation.md`, find the `## Implementation Steps` section,
   and build a TODO list — one per `### Step N:` — applying each step's current status.
   - If some steps are already `[x] done`, ask whether to resume from the next pending step or
     restart from the beginning.
   - If all steps are pending, prepend an environment-readiness check (delegate a quick
     dependency check to `developer` and `qa`).

2. **Per step**, run the cycle with the **Task tool**:
   1. `Task(subagent_type: "developer", …)` — implement the step. Pass `task_name`,
      `step_id`, `step_title`, and the path to `implementation.md`.
   2. **If code changed** → `Task(subagent_type: "developer-reviewer", …)`. On
      `needs_revision`, send the findings back to `developer` and repeat. Any suggestion
      triggers a revision; if it ping-pongs, cap the loop and intervene.
   3. `Task(subagent_type: "qa", …)` — **always**, even when no code changed. QA verifies the
      acceptance criteria and marks the step `[x] done`. On `needs_revision`, send it back to
      `developer`.
   4. Confirm the step is `[x] done` in `implementation.md`, then mark the TODO done.

3. **Repeat** until every step is done and QA-verified. Do not stop early.

4. **Reflect (mandatory final step).** `Task(subagent_type: "reflect", …)` with the task
   name, which agents ran, and what was accomplished. You are done only after it completes.

Delegate the real work to subagents and relay each result to the user as you go. Subagents
return only their final message, so trust the status they report (per the conventions).
