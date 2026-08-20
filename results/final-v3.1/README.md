# Final Study 2 dataset (v3.1, frozen)

This directory is the frozen dataset used by the associated EMSE / SECUTE 2026
manuscript. It contains the complete evidence for **120 candidates**: 12 .NET 10
security-repair tasks × 2 model configurations × 5 independent repetitions.

No result value in this directory was regenerated, recomputed, filtered or
edited after the final outcomes were observed.

## Provenance

| Property | Value |
|---|---|
| Experiment ID | `emse-study2-v3-replacements-full-gh32289459168-a1-f79cd61861eb` |
| Run type | `full` (120 candidates) |
| Repository commit | `f79cd61861ebebd2e7417d0e7602a1bccc551752` |
| GitHub Actions run | `32289459168`, attempt 1 |
| Started (UTC) | 2026-08-19T18:49:03Z |
| Protocol version | v3.1 (replacement-file edits; Reject and Escalate reported separately) |
| Model A | `gpt-5.6-sol` (OpenAI), 5 repetitions per task |
| Model B | `gpt-5.6-terra` (OpenAI), 5 repetitions per task |
| .NET SDK | 10.0.111 |
| CodeQL | 2.26.3, suite `codeql/csharp-queries:codeql-suites/csharp-security-extended.qls` |
| Git | 2.55.0 |

The runner configuration used for this run is preserved verbatim in
`run-metadata/experiment-config.json`, and the captured toolchain versions in
`run-metadata/environment.txt`.

## Headline results

- 120/120 edits applied
- 119/120 functionally correct
- 109/120 security correct
- 109/120 jointly correct
- 11 defective candidates, of which 1 was automatically rejected and 10 were false negatives
- 0 jointly correct candidates automatically rejected
- 40/120 candidates escalated for human review

Escalation is a governance outcome requiring review, not an automatic rejection,
and is therefore not counted as a false positive. See `../../docs/methodology.md`.

## Summary files

| File | Contents |
|---|---|
| `candidate-results.csv` | One row per candidate; the primary analysis table |
| `model-summary.csv` | Per-model rates, including separate reject and escalation rates |
| `task-summary.csv` | Per-task and per-CWE outcomes |
| `gate-summary.csv` | Per-gate pass/reject/escalate/indeterminate counts and latency |
| `detection-summary.csv` | Detection performance across all 120 candidates |
| `verification-only-summary.csv` | Detection performance among applied edits only |
| `leave-one-gate-out.csv` | Ablation: sensitivity and specificity with each gate removed |
| `first-flagged-gate.csv` | Which gate first rejected or first escalated each candidate |
| `paper-table-study2.md` | Manuscript table, generated from the same data |
| `plan.json` | The 120-candidate execution plan |
| `review-blinded.csv` | Blinded pack for human reviewers |
| `review-private-map.csv` | Blind-ID to candidate mapping; keep separate until adjudication ends |

## Candidate-level evidence

`candidates/<task>__<model>__r<NN>/` contains, for each of the 120 candidates:

| File | Contents |
|---|---|
| `prompt.txt` | The exact prompt sent to the model |
| `raw-model-output.txt` | The model's unmodified reply |
| `raw-provider-response.txt` | The full provider API response, including request ID |
| `candidate.edits.json` | The structured replacement plan the model returned |
| `candidate.diff` | The Git diff derived by the verifier after applying the plan |
| `result.json` | Per-gate outcomes, timings, and oracle results |
| `codeql.sarif` | CodeQL findings (present for 119 candidates; one run was indeterminate) |

The hidden functional and security oracles are recorded in `result.json` as
`oracle-functional` and `oracle-security`. They were executed only *after* the
automatic decision and never contributed to it.

## What is not included, and why

The complete Actions artifact is approximately 755 MB compressed and 5.4 GB
expanded. Two components are omitted here because they are bulk build state
rather than evidence, and both are reconstructible:

- `candidates/*/workspace/` (149 MB) — the per-candidate checkout. Equivalent to
  the task's `repo/` directory in this repository with `candidate.edits.json`
  applied; `candidate.diff` records exactly what changed.
- `candidates/*/codeql-db/` (5.2 GB) — CodeQL databases. The analysis output is
  retained as `codeql.sarif`.

The original artifact also contained a 44 MB `artifact-sha256.txt` covering those
omitted files. It is replaced here by `MANIFEST-sha256.txt`, which covers every
data and evidence file published in this directory (this README is excluded so
that documentation edits cannot invalidate the data checksums).

The complete original artifact remains downloadable from GitHub Actions run
`32289459168` until its retention period expires.

## Integrity

Every file in this directory was copied byte-for-byte from the Actions artifact
and verified with `cmp` before being committed. To re-verify:

```bash
cd results/final-v3.1
shasum -a 256 -c MANIFEST-sha256.txt
```

The reported figures can be recomputed from `candidate-results.csv` alone; see
[../../docs/reproduction.md](../../docs/reproduction.md).
