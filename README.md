# Verification-Gated LLM Patch Generation

[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.22033750.svg)](https://doi.org/10.5281/zenodo.22033750)

Replication package for the EMSE / SECUTE 2026 manuscript **"Verification-Gated
LLM Patch Generation: An Empirical Evaluation of Security Controls in .NET CI/CD
Pipelines."**

It contains the complete benchmark, the experiment runner, the exact workflow
used for the final run, and the frozen 120-candidate Study 2 dataset with
per-candidate evidence.

The frozen dataset is in [`results/final-v3.1/`](results/final-v3.1/). It can be
re-verified in a few minutes without an API key, an SDK, or any model call: see
[Reproduction](#reproduction).

## Overview

An LLM-generated patch is treated as an **untrusted contribution**. The question
is not whether a model can produce a plausible security fix, but whether an
automated pipeline of the kind a real .NET team would operate can tell an
adequate fix from an inadequate one before a human reads it.

The design rests on three separations:

- **Generation is separated from verification.** The model proposes an edit and
  has no influence over how it is judged.
- **Automatic verification is separated from ground truth.** The gate stack
  reaches one of three decisions — `Pass`, `Reject` or `Escalate` — and commits
  to it before any ground truth is consulted.
- **Ground truth is hidden during generation.** Independent functional and
  security oracles, never shown to the model, determine whether a candidate was
  actually correct. They run only after the automatic decision and never
  contribute to it.

That last ordering is what makes the headline measurement meaningful: the
pipeline's sensitivity is the rate at which it rejected candidates that the
oracles later proved defective.

## Study 2 Design

| Property | Value |
|---|---|
| Tasks | 12 purpose-built .NET 10 security-repair tasks, each with a distinct CWE |
| Models | 2 OpenAI configurations: `gpt-5.6-sol`, `gpt-5.6-terra` |
| Repetitions | 5 independent generations per task per model |
| Candidates | 120 |

The model returns a **structured replacement-file edit** — complete contents for
existing repository files — rather than a unified diff:

```json
{"files":[{"path":"Target/Example.cs","content":"complete replacement contents"}]}
```

The runner validates the plan, applies it, and then **derives the Git diff
itself**. The diff is therefore an artefact of the verifier rather than of the
model, and remains comparable across candidates and usable for blinded review.

The hidden functional and security oracles are not provided to the model. It
sees only the vulnerable repository, the public regression tests, the issue
description and the expected security property.

## Verification Pipeline

Gates run in this order:

1. **Structured edit validation** — plan is well-formed, in scope, and changes something
2. **Repository policy** — diff-scope and content rules
3. **`dotnet restore --locked-mode`** — dependencies match the committed lockfile
4. **`dotnet build -c Release`** — the change compiles
5. **Public regression tests** — the visible tests still pass
6. **NuGet audit** — `NU1903`/`NU1904` advisories are rejection evidence
7. **Security proof tests** — a hidden test carrying one known exploit case
8. **CodeQL** — `csharp-security-extended`, pinned to CLI 2.26.3 by checksum
9. **Independent hidden functional and security oracles**

**Step 9 is ground truth, not an acceptance gate.** The oracles execute only
after the automatic decision has been recorded, and never feed into it. Treating
them as a gate would leak the answer key into the detector being evaluated.

Only `Reject` counts as an automatic defect detection. `Escalate` means policy
requires human review; it blocks nothing and is reported separately as a review
cost, not as a false positive.

## Final Results

From [`results/final-v3.1/`](results/final-v3.1/), 120 candidates:

| Outcome | Count |
|---|---|
| Edits applied | 120 / 120 |
| Functionally correct | 119 / 120 |
| Security correct | 109 / 120 |
| Jointly correct | 109 / 120 |
| Defective candidates | 11 |
| Defective and automatically rejected | 1 |
| False negatives | 10 |
| Jointly correct candidates automatically rejected | 0 |
| Escalated for review | 40 / 120 |

By model:

| Model | Jointly correct |
|---|---|
| `gpt-5.6-sol` | 54 / 60 (90.0 %) |
| `gpt-5.6-terra` | 55 / 60 (91.7 %) |

The two models differ by one candidate out of sixty. **No statistically
meaningful difference in accuracy between the models is claimed**, and the study
was not designed or powered to detect one.

## Important Residual Finding

**All ten open-redirect (CWE-601) candidates applied successfully, passed the
entire automatic verification stack, and then failed the independent hold-out
security oracle.** They constitute ten of the eleven defective candidates and
all ten false negatives.

The generated repairs defeated the specific exploit shape encoded in the gate
security-proof test without establishing the general invariant that a redirect
target must be internal. Neither the targeted security test nor generic CodeQL
analysis distinguished "this particular redirect is blocked" from "redirects are
constrained".

This result was **retained rather than tuned away**. Strengthening the gate test
with the oracle's cases would have raised measured sensitivity and destroyed the
finding. It demonstrates residual semantic-security risk: when the relevant
invariant is not sufficiently encoded in generic CI controls, a plausible,
building, test-passing patch can still be insecure.

The remaining defective candidate was a Zip Slip (CWE-22) repair, and it was the
study's only automatic rejection — caught by the **build** gate because it did
not compile, rather than by any security-specific check.

## Pilot and Frozen Protocol

The final protocol is **v3.1**. It was reached through two corrections to the
measurement apparatus, both made *before* the final run.

**Unified diffs created a generation-format confound.** The initial pilot asked
models for a unified diff applied with `git apply`. A substantial share of
candidates were rejected because the diff would not apply — wrong context lines,
miscounted hunks, whitespace. Those failures are real, but they are failures of
output formatting, and they occurred *before* any security verification ran.
Reporting them alongside security rejections would conflate "cannot produce a
valid diff" with "cannot produce a secure repair". The final protocol therefore
uses structured replacement-file edits, and the verifier generates the actual
Git diff. In the final run all 120 edits applied, so every candidate reached the
security gates.

**v3.1 reporting separates `Reject` from `Escalate`,** so that mandatory-review
outcomes are not counted as automatic defect detections.

After these mechanisms were validated, the protocol was **frozen** and the
confirmatory 120-candidate run was executed. No tasks, prompts, gates, oracles,
thresholds or model configurations were altered after the final results were
observed.

**Pilot runs are not part of the final Study 2 dataset** and are not included in
`results/`. Their contemporaneous notes are preserved in
[`docs/pilot-history/`](docs/pilot-history/) solely to document how the protocol
reached its final form.

## Repository Layout

| Path | Contents |
|---|---|
| [`benchmarks/`](benchmarks/) | The 12 tasks. Each has `repo/` (model-visible: vulnerable `Target/` plus `PublicTests/`), `gate-security-tests/` (exploit case used as a gate), `hidden-functional-tests/` and `hidden-security-tests/` (hold-out oracles), and `task.json`. |
| [`src/ExperimentRunner/`](src/ExperimentRunner/) | The runner: prompt construction, edit validation and application, gate stack, oracle execution, aggregation. |
| [`config/`](config/) | Example configurations. Real run configs are gitignored. |
| [`scripts/`](scripts/) | Preflight structural checks, run-config generation, environment capture. |
| [`.github/workflows/run-experiment.yml`](.github/workflows/run-experiment.yml) | The workflow that produced the final dataset. |
| [`results/final-v3.1/`](results/final-v3.1/) | **The frozen dataset**: aggregate tables plus per-candidate evidence for all 120 candidates. |
| [`docs/`](docs/) | Methodology, reproduction guide, protocol, pilot history. |

The oracles are published here because the experiment is complete and frozen.
Anyone re-running the study must keep them out of the prompt; the runner
enforces this and `scripts/preflight.py` fails if oracle code is reachable from
the model-visible repository.

## Reproduction

See **[docs/reproduction.md](docs/reproduction.md)**, which covers three levels
of effort:

1. **Re-verify the published results** — no SDK, no API key, minutes.
2. **Regenerate every aggregate table** from the archived per-candidate evidence
   — .NET SDK only, no model calls. All eleven files reproduce byte-identically.
3. **Re-run the experiment** — API key, real cost, hours. Generation is
   stochastic, so a re-run will not match the published dataset candidate for
   candidate.

Detailed method, including threat model and decision semantics, is in
**[docs/methodology.md](docs/methodology.md)**. The pre-registered objective,
research questions and analysis plan are in
**[docs/protocol.md](docs/protocol.md)**.

## Results

The frozen dataset is **[`results/final-v3.1/`](results/final-v3.1/)**, with its
own [README](results/final-v3.1/README.md) describing provenance, file-by-file
contents, and what was omitted for size.

Start with `candidate-results.csv`: every figure in the manuscript derives from
it. `MANIFEST-sha256.txt` covers all 854 published evidence files.

## Security and Credentials

**No credentials are included in this repository**, and none appear anywhere in
its history. Reproduction requires you to supply your own `OPENAI_API_KEY`
through GitHub Actions Secrets or your local environment. The workflow reads it
only via `${{ secrets.OPENAI_API_KEY }}` and fails early with an explanatory
message if it is absent.

The benchmark tasks intentionally contain vulnerable code and fixture strings
such as `password=`. These are the subject of the study, are inert, and are not
a credential leak.

## Citation

If you use this replication package, please cite the associated paper using the
metadata in [`CITATION.cff`](CITATION.cff).

This package is archived on Zenodo. Cite the **concept DOI**, which always
resolves to the most recent version:

**Concept DOI:** [10.5281/zenodo.22033750](https://doi.org/10.5281/zenodo.22033750)

That DOI is what the manuscript cites. It always resolves to the newest archived
version, and its Zenodo page lists every version with its own version DOI if you
need to cite an exact snapshot.

Releases after v1.0.0 correct documentation only. The dataset in
`results/final-v3.1/` is byte-identical across all versions, so every figure
reported in the manuscript holds for any of them.

## Licence

Released under the MIT Licence. See [`LICENSE`](LICENSE).
