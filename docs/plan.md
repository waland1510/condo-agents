# Plan

The repository answers one interview question: *what have you built with AI
agents?* Everything below is ordered so each step leaves the repo green and
readable on its own.

## Target shape

A multi-agent condo due-diligence system. A buyer uploads a building's
documents (bylaws, AGM minutes, financial statements, reserve-fund study,
mostly in French) and asks whether the purchase is safe.

- **Planner** decomposes the question into a typed plan.
- **Specialists** execute plan steps with scope-bound tools: retrieval,
  fact-sheet extraction, a deterministic finance calculator, the public
  Civil Code of Québec reference corpus.
- **Critic** checks every claim against retrieved evidence and can send the
  work back once; a non-LLM groundedness check runs after it.
- **Human-in-the-loop** at two points: PII findings before indexing, and any
  tool marked irreversible. The run persists and resumes on approval.
  Run state is a JSON file per run (`FileRunStore`); Postgres was planned
  and dropped because a file store gives the same restart guarantee with no
  container.
- **Guardrails as code**: PII guard before any model call, Prompt Shields on
  untrusted input, per-run budgets, fail-closed configuration, citations
  resolved against what was actually retrieved.
- **Trajectory evals** assert on the path (tools called, scope respected,
  seeded hallucination caught), with results committed to the repo.
- **Observability**: one trace per run, a span per agent and tool call,
  tokens and cost per span.

Everything runs offline on in-memory fakes; Azure is a configuration switch.

## Steps

| # | Step | Done when |
|---|---|---|
| 1 ✅ | Scaffold: solution, domain, fakes, fail-closed API, CI | `dotnet test` green with no network; CI badge green |
| 2 ✅ | Seed data: one fictional building, FR/EN documents with seeded PII, a contradiction and a false claim; Civil Code articles | Documents checked in under `data/` with a manifest |
| 3 ✅ | Ingest tools: translate, crack, PII with role adjudication, attest gate, index with scope metadata | Pipeline test runs end to end on fakes |
| 4 ✅ | Single agent with scope-bound tools and budgets | Tool contract test proves scope cannot be widened; ADR 2 |
| 5 ✅ | Planner, specialists, critic; typed plan; run persistence | Replayable run JSON; critic catches the seeded false claim |
| 6 ✅ | Human-in-the-loop: pause, approve, resume; irreversible-tool marker | Run survives a process restart mid-approval |
| 7 ✅ | Trajectory evals, committed results | `evals/results/*.json` and a summary table in the README |
| 8 ✅ | MCP server, README diagram, reading guide, thin CLI | A reader can follow the five-minute guide without running anything |
| 9 ✅ | Real Azure clients behind the same interfaces; env-gated eval run against the pinned model | `Ai:UseAzure=true` boots against Foundry, Search, Language and Content Safety; `evals/results/azure-*.md` committed |

## Conventions

- .NET 10, Microsoft Agent Framework kept at the edges; agents, tools,
  gates and the plan are plain classes with a thin adapter.
- No comments that restate code. A `/// <summary>` only where a constraint
  or gotcha is invisible from the code.
- Every gate has a unit test. Every decision has an ADR.
