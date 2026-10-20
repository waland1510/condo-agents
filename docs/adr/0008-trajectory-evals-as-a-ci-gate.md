# 8. Evals grade the trajectory, run on the fakes in CI, and their results are committed

Date: 2026-10-20 · Status: accepted

## Context

An agent system can produce a fine-looking answer by the wrong path: a
specialist that never searched, a scope it should not have touched, a tool
it should have been stopped from calling, a critic that never ran. Grading
prose does not see any of that.

## Decision

`evals/cases.json` holds cases whose expectations are all about the run
record: final status, the specialists the planner chose, tools called and
not called, documents cited and documents that must never appear in
evidence, pending approvals, the critic's rejection reasons, the error on a
failed run, and a ceiling on model calls. Nothing asserts on wording.

`EvalRunner.Grade` reads only the persisted `AgentRun`, so a grade can be
reproduced from the run JSON without re-running anything.

The suite runs three ways:

- `dotnet run --project src/CondoAgents.Evals` writes
  `evals/results/latest.json` and `latest.md`, which are committed so a
  reader sees numbers without running anything.
- `EvalSuiteTests` runs every case as an xUnit theory, so CI fails on a
  regression. On the scripted fake the cases are deterministic.
- The same runner takes a real model through the `factory` delegate; that
  mode is env-gated and not yet wired (step 9 in the plan).

## What the first run found

The `tool-budget-fails-closed` case failed on its first run: the run did
fail, but on the model-call ceiling after 12 calls, not on the tool ceiling
after 1. The function-calling loop reports a tool exception to the model as
an error result and keeps going, so the budget violation thrown in the tool
middleware never reached the orchestrator. The fix sets
`FunctionInvocationContext.Terminate`, records the violation on the trace,
and rethrows it after the loop returns (`AgentFactory.RunCheckedAsync`).
The unit test had not caught this because its runaway model produced
consecutive errors, which the loop rethrows on its own after three.

## Rejected

- **LLM-as-judge for correctness.** Useful later against a real model, but
  it would make the CI gate non-deterministic and would judge prose.
- **Expectations on answer text.** The fake model's prose is first sentences
  of passages; asserting on it would test the fake.
- **Results generated in CI only.** A reader on GitHub never sees CI
  artefacts; the committed files are the point.

## Consequences

Every gate and every orchestration rule now has a case that exercises it
end to end, and the first run already paid for itself.
