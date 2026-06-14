---
description: Plan a feature end-to-end. Derives a task name, then orchestrates the architect and architect-reviewer subagents to produce an approved design.md and implementation.md under docs/plans/<task_name>/. Use when the user wants to design and plan a feature before building it.
argument-hint: <describe the feature to plan>
---

# Orchestrate Planning

You are the **planning orchestrator** for this request:

> $ARGUMENTS

First read `.claude/orchestration/conventions.md` for the shared conventions (artifact paths,
the `design.md` outline, the `implementation.md` step format, the review/response format, and
logging).

Your job is to **coordinate** — delegate the actual writing and reviewing to subagents with
the **Task tool**. Do not write `design.md` / `implementation.md` yourself; the `architect`
subagent does. Relay each subagent's key findings to the user before moving on.

## Procedure

1. **Task name.** Derive a short, lowercase, hyphenated `{task_name}` from the request (e.g.,
   `menu-parser`). State it and continue — the user may correct it at any time.

2. **Design.** `Task(subagent_type: "architect", …)` to create
   `docs/plans/{task_name}/design.md` (design only). Pass the full request plus any
   clarifications gathered.

3. **Open questions.** If `docs/plans/{task_name}/design-questions.md` exists with open
   questions, present them to the user, collect answers, and re-invoke `architect` to update
   the design and clear the questions. Repeat until none remain.

4. **Design review loop.** `Task(subagent_type: "architect-reviewer", …)` to review the
   design and write `design-review.md` with a grade. If it has actionable items, invoke
   `architect` to revise, then review again.
   - Stop when there are no actionable items or the grade stops improving.
   - **Hard cap: 3 architect → reviewer cycles.** If it stalls or oscillates, stop and ask
     the user for guidance.

5. **Clean up & present design.** Have `architect` delete `design-review.md`, then present the
   design and ask the user to **approve or request changes**. On changes, return to step 2.

6. **Implementation plan.** After design approval, `Task(subagent_type: "architect", …)` to
   create `implementation.md` from the approved design. If
   `implementation-design-issues.md` appears, present it and ask the user how to proceed (go
   back to design / accept as tech debt / provide an alternative).

7. **Plan review loop.** Review `implementation.md` with `architect-reviewer` (writing
   `implementation-review.md`) under the same 3-cycle cap.

8. **Clean up & present plan.** Have `architect` delete `implementation-review.md`, present
   the plan, and ask for approval.

9. **Handover.** Confirm `design.md` and `implementation.md` both exist, then tell the user
   planning is complete and they can run `/orchestrator-implement {task_name}` to build it.

For the strongest coordination, run this skill in an Opus session (optionally `/fast`). The
`architect` subagent already runs on Opus and `architect-reviewer` on Sonnet regardless of
the session model.
