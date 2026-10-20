# 3. A synthetic corpus whose traps are declared, not hidden

Date: 2026-10-19 · Status: accepted

## Context

The system has to be judged on what it refuses to do: leak an owner's name,
present a superseded bylaw as current, report a rejected proposal as a
decision. Real condo documents cannot be published, and a corpus whose traps
live only in the author's head cannot be tested.

## Decision

One fictional building, `data/buildings/jardins-plateau/`, with six
documents in the languages a Montréal buyer actually receives: bylaws in two
versions, two sets of AGM minutes, audited financial statements in English
and a reserve-fund study. A condensed Civil Code reference corpus sits beside
it, shared across buildings and clearly labelled as a working summary.

`data/seeds.json` declares every trap and every truth:

- **PII** with the role the adjudicator must assign (an owner in arrears is
  redacted; the property manager and the engineer are kept).
- **Contradictions** across documents with the version that is current.
- **Lures**: claims a careless reader would make, each with the marker in
  the text that disproves it (`REJETÉE`).
- **Facts** a fact sheet must extract, with the documents that support each.

`tests/CondoAgents.Tests/Corpus/SeedsTests.cs` pins seeds to documents, so
an edit that silently removes a planted fact fails a unit test rather than
quietly weakening an eval.

## Rejected

- **Real documents, anonymised.** Anonymisation is the hard part of the
  problem; doing it by hand for test data proves nothing.
- **Model-generated documents at test time.** Non-deterministic; the traps
  would move between runs.
- **Traps known only to the evals.** The evals should import the seeds, not
  own them, so the same declaration drives unit tests, evals and the README.

## Consequences

Every later layer (redaction, extraction, the critic, trajectory evals) has
a ground truth to assert against from day one, and a reader can open one
JSON file to see what the system is expected to catch.
