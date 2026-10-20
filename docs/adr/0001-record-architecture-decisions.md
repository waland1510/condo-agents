# 1. Record architecture decisions

Date: 2026-10-19 · Status: accepted

## Context

This repository exists to show how a multi-agent system can be built so that
the model drives control flow without owning any security or correctness
property. Most of that value is in decisions, not features, and decisions
without a written rationale are indistinguishable from accidents.

## Decision

Every non-obvious decision gets a short record in `docs/adr/`, numbered,
with the options that were rejected and why. A record is never edited after
acceptance; a change of mind is a new record that supersedes it.

## Consequences

A reader can follow the reasoning in order. Each ADR links to the code that
enforces it and to the test that would fail if the decision were violated.
