# 6. Planner, specialists and critic, with the orchestrator deciding what is kept

Date: 2026-10-20 · Status: accepted

## Context

A single agent with every tool answers narrow questions well and broad ones
badly: it searches once, cites what it found and stops. "Is this condo safe
to buy" needs money, governance and law looked at separately, and needs
someone to check the result before the buyer sees it.

## Decision

`DueDiligenceOrchestrator` runs three kinds of agent through the same
`AgentFactory` (same budget, same trace):

- **Planner** returns a typed `Plan` (structured output). `Plan.Validate`
  rejects unknown specialists, duplicate ids, cycles and more than six steps
  before anything runs, so a bad plan costs one model call.
- **Specialists** get a narrowed toolbox: `finance` has search and the
  deterministic projection, `legal` has only the Civil Code lookup,
  `documents` has everything plus the approval-gated contact tool. Narrowing
  is a subset of the scoped tools, never a new scope.
- **Critic** receives the specialist's claims (sentences with their labels)
  and the evidence passages, and returns a verdict per claim. A failing
  verdict sends the step back once with the rejected claims in the prompt.
  After the second attempt the orchestrator keeps only claims the critic
  supported and reports the rest as dropped.
- **Synthesis is code.** The final answer is the kept claims per step with
  their verified citations. No model gets the last word on what is cited.

The seeded lure (the rejected charging-station vote) is the acceptance test:
a specialist scripted to assert it was installed is sent back, repeats, and
its claim is dropped from the final answer.

## Rejected

- **Agent Framework workflows for the graph.** The plan is data chosen at
  run time, and the orchestration is a dozen lines; a workflow graph would
  move the control flow into a framework abstraction the reader then has
  to learn. Revisit when steps need to run concurrently.
- **Letting the critic rewrite the answer.** A critic that edits is a second
  author; a critic that only accepts or rejects is auditable.
- **Unbounded send-back.** Two attempts per step keeps the worst case
  inside the run budget and makes the trajectory evals deterministic.

## Consequences

Every run records plan, attempts, verdicts, kept claims and dropped claims,
so the evals in step 7 can assert on the path rather than on prose.
