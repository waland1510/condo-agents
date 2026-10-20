# 5. Budgets, gates and citation checks live outside the model's loop

Date: 2026-10-20 · Status: accepted

## Context

Once the model drives control flow, the system's worst case is no longer
"wrong answer" but "runs forever", "calls a tool it should not", or
"cites something it never read". Instructions in the prompt cannot bound any
of these; they are advice to the thing being bounded.

## Decision

Every agent is built by `AgentFactory.Create`, which layers:

- **`BudgetedChatClient`** under the framework's function-calling loop. It
  counts model calls and tokens per run and throws `BudgetExceededException`
  when `RunBudget` is exceeded. Because it sits below the loop, the ceiling
  holds however many turns the model requests.
- **Function-invocation middleware** above the loop, counting tool calls
  against the same budget and recording every call in `RunTrace`.
- **`Gates`** before the first model call: a PII detector on the question
  and Prompt Shields on it. A refusal here costs zero tokens and leaves a
  trace event, so an eval can assert the model was never reached.
- **Prompt Shields on retrieved passages inside the tool**, withholding
  attacked chunks before they are handed back as evidence.
- **`CitationValidator`** after the run: labels in the structured answer
  are resolved against the toolbox's evidence list; anything else is
  reported as rejected, never rendered as a citation.
- **A wall-clock cap** through a linked cancellation token.

Tests: `tests/CondoAgents.Tests/Agents/DueDiligenceAgentTests.cs` includes a
deliberately runaway model (`LoopingChatClient`) and a model that cites a
label it never received.

## Rejected

- **Telling the model to stop after N tools.** Not enforceable.
- **Budget inside the agent's instructions or the tool descriptions.** Same.
- **Validating citations by string-matching the prose.** Labels are
  positional keys into a list the toolbox owns; prose matching is fuzzy and
  the model can paraphrase.

## Consequences

The planner, specialists and critic in step 5 reuse the factory unchanged,
so each inherits the same ceilings and trace. A new agent cannot be created
without them.
