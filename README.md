# condo-agents

[![ci](https://github.com/waland1510/condo-agents/actions/workflows/ci.yml/badge.svg)](https://github.com/waland1510/condo-agents/actions/workflows/ci.yml)

A multi-agent system for condo due diligence, built to show one thing: an
LLM can drive the control flow of a system without owning any of its
security or correctness properties.

A buyer uploads a building's documents (bylaws, AGM minutes, financial
statements, a reserve-fund study, mostly in French) and asks whether the
purchase is safe. A planner agent decomposes the question, specialist agents
answer each part through tools that carry their access scope with them, a
critic agent checks every claim against the evidence, and a human approves
anything irreversible.

Status: step 1 of the [plan](docs/plan.md). The scaffold, the scope boundary,
citation validation, fail-closed configuration and the offline fakes are in
place and tested. Agents come next.

## Run

```bash
dotnet test --solution CondoAgents.slnx        # no network, no Azure
dotnet run --project src/CondoAgents.Api       # http://localhost:5027/health/ready
```

Outside `Development` the API refuses to boot on fakes
(`Ai:RequireRealDependencies`), so a half-configured deployment fails loudly
instead of serving canned answers behind a healthy probe.

## Reading guide

1. [docs/plan.md](docs/plan.md): what is being built and in what order.
2. [docs/adr/](docs/adr/): why it is built this way. Start with
   [ADR 2, scope lives in tools](docs/adr/0002-scope-lives-in-tools-not-in-prompts.md).
3. `src/CondoAgents.Domain/Scoping/ScopeFilter.cs` and its tests: the
   retrieval boundary, built in code per request.
4. `src/CondoAgents.Clients/Fakes/`: the offline implementations. The
   in-memory retrieval client honours the same filter as Azure AI Search, so
   a scope test means the same thing in both modes.

## Layout

```
src/CondoAgents.Domain      scope, retrieval and citation types; no dependencies
src/CondoAgents.Clients     external-service abstractions and their fakes
src/CondoAgents.Api         minimal API, configuration validation, telemetry
tests/CondoAgents.Tests     xUnit v3 on Microsoft.Testing.Platform
docs/adr                    architecture decision records
```
