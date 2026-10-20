# 7. Irreversible tools pause the run through the framework's approval flow

Date: 2026-10-20 · Status: accepted

## Context

One tool leaves the system: `send_question_to_syndic`. An agent that can
call it on its own judgement is an agent that can email a stranger. The
pause has to survive a process restart, because the approver is a person
on their own schedule.

## Decision

- The tool is wrapped in `ApprovalRequiredAIFunction`. The function-calling
  loop then returns a `ToolApprovalRequestContent` to the orchestrator
  instead of invoking anything.
- The orchestrator records a `PendingApproval` (request id, tool,
  arguments), serializes the agent session into the step record, sets the
  run to `AwaitingApproval` and persists it.
- `DecideAsync` loads the run from the store, marks the decision, rebuilds
  the agent, deserializes the session and runs it with a
  `ToolApprovalResponseContent`. Only then does the tool execute, and only
  if approved. A rejected decision ends the run as `Rejected` with nothing
  sent.
- A decision can be made once; a second attempt is refused.

`HumanInTheLoopTests` creates the run with one orchestrator and file store,
then resumes it with a fresh orchestrator over the same directory.

## Rejected

- **Throwing from inside the tool to pause.** The function-calling loop
  catches tool exceptions and reports them to the model; the run would
  continue with the model deciding what to do about the error.
- **Asking the model to ask permission.** Not enforceable.
- **Holding the paused run in memory.** Approval latency is human latency.

## Consequences

Any future side-effecting tool gets the same wrapper and inherits the pause.
The PII screening report from step 3 is the other approval surface; it uses
the attestation hash rather than this flow because no agent is involved.
