# 2. Scope lives in tools, not in prompts or tool arguments

Date: 2026-10-19 · Status: accepted

## Context

Agents call tools with arguments the model chooses. If the retrieval tool
took a `buildingId` argument, the access boundary would depend on the model
choosing the right value, and a prompt injection in a retrieved document
could ask it to choose another one. Telling the model "only search building X"
in the system prompt has the same weakness: it is advice, not enforcement.

## Decision

- The caller resolves a `Scope` (`src/CondoAgents.Domain/Scoping/Scope.cs`)
  before any agent runs. Agents and tools receive it; nothing in the agent
  layer can construct or widen one.
- `ScopeFilter.Build` turns the scope into the search filter in code. A
  malformed scope throws (`ScopeException`) rather than producing an open
  filter. Tests: `tests/CondoAgents.Tests/Domain/ScopeFilterTests.cs`.
- Tools are constructed with the scope already bound. Their public signature
  exposes only what the model is allowed to choose (the query, the document
  to open), never the boundary.
- The fake retrieval client parses and honours the same filter
  (`InMemoryRetrievalClient`), so a scope test gives the same verdict offline
  as against Azure AI Search.

## Rejected

- **Scope as a tool argument validated after the fact.** Validation is a
  second place to get wrong, and the model still sees the boundary as
  something it chooses.
- **Scope in the system prompt only.** Not enforceable; Prompt Shields reduce
  injection risk but do not eliminate it.
- **One index per building.** Operationally heavier, and the per-request
  filter is the same code path whether there is one index or many.

## Consequences

Adding a specialist agent means adding tools bound to the same scope. A
tool that could reach outside the scope has to be written deliberately, and
the contract test for tools (to come with the agent layer) fails on it.
