# 4. Screen, attest, then index; the attestation binds to the redacted text

Date: 2026-10-20 · Status: accepted

## Context

AGM minutes name owners in arrears and litigants. The retrieval index is what
every agent reads, so anything indexed is effectively what the model can
say. A buyer is entitled to the documents, not to re-publishing other
owners' personal data through an assistant.

## Decision

`IngestPipeline` runs in a fixed order, with no step the model can skip:
crack, translate, detect PII, adjudicate roles, redact, hash.

- **Role adjudication, fail closed.** A detected person is kept only when the
  adjudicator returns `professional` (manager, engineer, lawyer, auditor) or
  `director` (board members acting as such). `owner` and `unknown` both
  redact. The adjudicator is the one model-backed step, and its failure
  mode is more redaction, never less.
- **Attestation binds to the screening hash**, computed over the exact
  redacted text that would be indexed. `IndexAsync` re-screens and refuses
  when the hash moved: an edited file cannot ride an old attestation.
- **Scope is validated before the file is opened.** A malformed scope throws
  `ScopeException` before any byte is read.
- **The reference corpus skips the gate** because it is public statute with
  no personal data by construction; it is the only path that bypasses
  screening, and it is a separate method so the bypass is visible.
- **The seed corpus goes through the real pipeline** at startup, self-attested
  as `seed:synthetic-corpus`. The fake index therefore never holds an owner's
  name, and a test proves it against `seeds.json`.

## Rejected

- **Index first, redact at answer time.** The model would still see the
  names; redaction after generation is a filter on output, not a boundary.
- **Attestation keyed by document id.** Replacing the file after attesting
  would index unscreened text under a valid attestation.
- **Trusting the detector alone.** It flags the property manager and the
  engineer too; without roles the documents lose the names a buyer needs.

## Consequences

A later real detector and model adjudicator slot in behind `IPiiDetector`
and `IRoleAdjudicator` without touching the order. The screening report is
the artefact a human approves in step 6.
