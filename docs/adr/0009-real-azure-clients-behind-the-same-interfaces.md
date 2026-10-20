# 9. Real Azure clients behind the same interfaces, on a shared service without sharing anything

Date: 2026-10-20 · Status: accepted

## Context

Steps 1 to 8 run on deterministic fakes. The claim that "every boundary
means the same thing offline as against Azure" is only a claim until the
real clients exist and the same eval cases run against a real model. The
Azure resources available are a dev Foundry account and an AI Search
service that another workload already uses.

## Decision

`CondoAgents.Clients.Azure` implements the existing interfaces and nothing
else changes above them:

| Interface | Azure implementation |
|---|---|
| `IChatClient` | Foundry `gpt-5-mini` through `Azure.AI.OpenAI` and the Microsoft.Extensions.AI adapter |
| `IRetrievalClient`, `IIndexWriter` | One push-mode AI Search index with `text-embedding-3-large` vectors computed client-side; hybrid query with the code-built filter |
| `IPiiDetector` | AI Language PII, person-identifying categories only |
| `IPromptShield` | Content Safety Prompt Shields over plain HTTP, five documents per call |
| `IRoleAdjudicator` | `ModelRoleAdjudicator`: one strict-JSON model call per batch |

Isolation on the shared service is enforced in code, not by convention:

- `AzureOptions.Validate` and the API's options validator refuse any index
  name that does not start with `condo-agents-`. The app cannot be
  configured to read or write another workload's index.
- The index is push-mode: no indexer, skillset, data source or vectorizer
  is created, so no role assignment on the Search or Foundry side changes.
- No blob storage. Document Translation and Document Intelligence need a
  storage account the service itself reads; this app stays on the identity
  translator and plain-text extractor, and says so.
- Credentials are Entra only. Locally the Azure CLI identity is pinned to
  one subscription because the developer's default tenant is not the one
  that owns the resources; a deployed instance would use managed identity
  through `DefaultAzureCredential`.

## What the first real run changed

- **Attestation no longer re-screens.** `IndexAsync` used to run the
  screening again and compare hashes. With a model-backed adjudicator the
  second screening is not byte-identical, so every index call failed with
  an attestation mismatch. The attestation now binds to the screening
  report, and indexing checks a hash of the source bytes instead. ADR 4's
  guarantee (an edited file cannot ride an old attestation) holds; the
  mechanism moved from "re-derive" to "verify inputs".
- **Chunk ids.** Search keys cannot contain `#`; ids are now `doc__n`.
- **Prompt Shields batching.** The service takes at most five documents
  per request; the client re-bases indexes across batches.
- **Outreach became a planner decision.** With the fake, the documents
  specialist only called `send_question_to_syndic` when scripted to. The
  real model reached for it on ordinary fee and bylaw questions, and the
  approval gate paused four of eight runs. That is the gate working, but a
  buyer asking about fees should not be asked to approve an email. The
  tool is now granted per plan step through `PlanStep.ContactSyndicate`,
  which the planner sets only when the buyer asked to contact the board.
- **Plans are capped per specialist.** The real planner produced five and
  six steps with repeated specialists; `Plan.Validate` now allows at most
  two steps per specialist and the prompt asks for the fewest steps.
- **Budgets and expectations tuned to the fake did not transfer.** A broad
  question costs the real model about 18 model calls and 24 tool calls,
  mostly `open_document`. Azure mode runs with a larger budget and the
  cases carry a separate `maxModelCallsAzure` ceiling. The specialists
  check compares sets, not order, because step order is the planner's.

- **Model-produced structures can omit arrays.** The real critic once
  returned `{"pass":false}` with no `unsupported` list and the orchestrator
  crashed on it. Every structured result (plan, answer, verdict) is now
  normalized before use, and a verdict with no specific rejection keeps
  the claims rather than dropping them all.
- **Two processes starting together raced on the index definition.** The
  index is now created once and left alone when it exists.
- **The broad question does not fit a budget the real model respects.**
  Two runs of "is this condo safe to buy" cost 18 and then 36 tool calls,
  mostly `open_document` on the same documents, and the second failed on
  the ceiling after eight minutes and 260k tokens. The committed result
  shows that failure. It is the budget working as designed; the fix is a
  specialist prompt that bounds its own reading, and that is future work.
- **The lure check assumed a gullible specialist.** The fake asserts the
  charging stations were installed and the critic catches it; the real
  model reads "REJETÉE" and says so, leaving the critic nothing to reject.
  The check now passes on either path and fails only when neither the
  answer nor the critic caught it.

- **Ceilings are run-to-run variance, not thresholds.** The committed run
  shows the pets question at 13 model calls against a ceiling of 10 because
  the critic sent the step back once; the run before it took 9. Six of
  eight cases pass on the real model and the two that fail are both cost,
  never scope, safety or grounding.

## What held without change

The PII gate, the scope boundary (the other building saw only the
reference corpus), the tool-budget fail-closed path and the approval pause
behaved identically. On the seeded lure the real model answered correctly
on its own ("comme la proposition a été rejetée, elle ne constitue pas une
décision"), and the real critic rejected a different claim in the same
answer, an assertion about which documents had been consulted that the
cited passage did not support. The fake critic would not have caught that.

## Rejected

- **An integrated vectorizer or skillset.** Convenient, but it needs a
  role assignment for the Search service's identity on the Foundry
  account, which touches shared infrastructure.
- **A separate Search service.** Cleaner still; the prefix rule gives the
  same guarantee at zero cost and documents the shared-service case, which
  is the realistic one.
- **The other workload's service principal.** Available in the dev
  environment, and the wrong identity to borrow.

## Consequences

The eval suite runs unchanged in both modes (`CONDO_EVAL_MODE=azure`), and
the committed `evals/results/azure-*.md` shows where a real model and the
scripted fake disagree. Expectations tuned to the fake (exact specialist
order, model-call ceilings) are the ones to read with care.
