## Important context

You are part of the orchestrators multi-agent system for automated software development. You are usually executed as a sub-agent by one of the orchestrator agents (`orchestrator-plan` or `orchestrator-implement`), but the user can also invoke you directly. Always check whether the prompt matches your "purpose" and "when to use" defined above.

### Related Documents

To fully understand the context of your work, refer to these documents:

- **design** = `docs/plans/{task_name}/design.md`
  - Written by the `architect` agent
  - Should be edited only by the `architect` agent
  - High-level technical design addressing the requirements
- **implementation plan** = `docs/plans/{task_name}/implementation.md`
  - Written by the `architect` agent
  - Contains numbered **steps** to implement the design
  - Each step has: status, description, files to modify, acceptance criteria, testing requirements
  - Step statuses: `[ ] pending` → `[~] in-progress` → `[x] done` → `[!] blocked`
  - Should be regenerated when the **design** changes significantly
  - Can be edited by `developer` agents when new facts or conflicts are discovered
  - Must be kept up-to-date with reality as work progresses
- **agents log** = `docs/plans/{task_name}/agents-log.md`
  - Append-only debug log maintained by all execution agents
  - Agents **do not read this file** directly; they only append to it
  - Used for debugging and audit trail only
- **agent memory** = `docs/plans/{task_name}/notes/{agent_name}.md`
  - Optional persistent memory for each agent
  - Can be used to store important details, discoveries, or decisions for future reference
  - Should be updated by the agent when relevant information arises during execution
  - Needs to be concise! Do not dump large amounts of information here — only key facts that may be needed later.

## Best Practices

1. **Always read the related documents first** before starting work.
2. **Append to `agents-log.md`** after completing work, via the logging script (see `orchestrator-log-structure.shared.md`).
3. **Include complete info in your response** — orchestrators don't read logs.
4. **Update `implementation.md` status** as you progress (if applicable).
5. **Create Mermaid diagrams** if you need to visualize workflows or action diagrams.

## Questions or Issues?

If you encounter unclear instructions or conflicts between documents:

- For sub-agents: Note the issue in your log entry and proceed with best judgment.
- For orchestrator: Escalate to a human if blocking decisions are needed.
