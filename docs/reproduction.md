# Reproduction guide

This guide covers three distinct things a reviewer might want to do, in
increasing order of cost:

1. [Re-verify the published results](#1-re-verify-the-published-results) without
   running anything — no SDK, no API key, minutes.
2. [Regenerate the aggregate tables](#2-regenerate-the-aggregate-tables) from the
   archived per-candidate evidence — .NET SDK only, no API key, no model calls.
3. [Re-run the experiment](#3-re-run-the-experiment) — API key, real cost, hours,
   and stochastic output that will not match ours exactly.

Most reviewers will only need the first two. Neither contacts a model provider.

## Prerequisites

| For | You need |
|---|---|
| Re-verifying results (1) | Python 3 or any CSV tool; `shasum` |
| Regenerating tables (2) | .NET SDK 10.0.111, Git |
| Re-running the study (3) | The above, plus an OpenAI API key, plus CodeQL CLI 2.26.3 if running locally |

The SDK version is pinned in `global.json` to `10.0.111` with
`rollForward: latestPatch`. That deliberately will **not** roll forward to a
different feature band, so a `10.0.3xx` SDK is rejected. This is intentional:
the pin is part of the experiment's reproducibility. Install `10.0.111` from
<https://dotnet.microsoft.com/download/dotnet/10.0>, or run via GitHub Actions,
where the workflow installs the pinned SDK for you.

Running the full study in GitHub Actions needs no local toolchain at all.

## Clone

```bash
git clone <repository-url>
cd exp001
```

## 1. Re-verify the published results

The frozen dataset is [`results/final-v3.1/`](../results/final-v3.1/). Confirm
nothing has been altered since publication:

```bash
cd results/final-v3.1
shasum -a 256 -c MANIFEST-sha256.txt
```

Recompute the headline figures from the candidate-level table. Every number in
the manuscript derives from this one file:

```bash
python3 - <<'PY'
import csv
rows = list(csv.DictReader(open("results/final-v3.1/candidate-results.csv")))
b = lambda r, k: r[k].strip().lower() == "true"
applied   = [r for r in rows if b(r, "edit_applied")]
joint     = [r for r in rows if b(r, "joint_correctness")]
defective = [r for r in applied if not b(r, "joint_correctness")]
print("candidates:      ", len(rows))
print("edits applied:   ", len(applied))
print("functional:      ", sum(b(r, "functional_correctness") for r in rows))
print("security:        ", sum(b(r, "security_correctness") for r in rows))
print("jointly correct: ", len(joint))
print("defective:       ", len(defective))
print("rejected:        ", sum(b(r, "automatic_rejected") for r in rows))
print("false negatives: ", sum(not b(r, "automatic_rejected") for r in defective))
print("escalated:       ", sum(b(r, "automatic_escalated") for r in rows))
PY
```

This should report 120 candidates, 120 applied, 119 functional, 109 security,
109 jointly correct, 11 defective, 1 rejected, 10 false negatives and 40
escalated.

## 2. Regenerate the aggregate tables

Every summary CSV is derived from the 120 `result.json` files under
`results/final-v3.1/candidates/`. You can rebuild all of them without contacting
a model.

> **Work on a copy.** The `aggregate` command writes its output into the results
> directory you point it at. Running it directly against `results/final-v3.1/`
> would overwrite the frozen dataset in place.

```bash
dotnet build EMSE.SecurityExperiment.sln -c Release

# Scratch copy: only the candidate evidence is needed as input.
mkdir -p /tmp/verify/results/final-v3.1
cp EMSE.SecurityExperiment.sln /tmp/verify/
cp -R results/final-v3.1/candidates /tmp/verify/results/final-v3.1/candidates

cat > /tmp/verify/agg.json <<'JSON'
{
  "experimentId": "final-v3.1",
  "benchmarkRoot": "benchmarks",
  "resultsRoot": "results",
  "models": [{"id":"model-a","provider":"openai","model":"gpt-5.6-sol"}]
}
JSON

cd /tmp/verify
DLL="$OLDPWD/src/ExperimentRunner/bin/Release/net10.0/ExperimentRunner.dll"
dotnet "$DLL" aggregate agg.json
dotnet "$DLL" blind-review agg.json
```

Then compare against the published tables:

```bash
cd -
for f in candidate-results.csv model-summary.csv task-summary.csv \
         gate-summary.csv detection-summary.csv verification-only-summary.csv \
         leave-one-gate-out.csv first-flagged-gate.csv paper-table-study2.md \
         review-blinded.csv review-private-map.csv; do
  cmp -s "/tmp/verify/results/final-v3.1/$f" "results/final-v3.1/$f" \
    && echo "identical: $f" || echo "DIFFERS:   $f"
done
```

All eleven files are byte-identical. The blinded review pack uses a fixed
shuffle seed, so it too reproduces exactly.

The `agg.json` above is a minimal configuration: aggregation reads only
`experimentId` and `resultsRoot` to locate the evidence, so the `models` entry
is a required placeholder and is not used to contact any provider.

## 3. Re-run the experiment

Re-running generates fresh candidates from the models. Because generation is
stochastic, results **will not** match the published dataset candidate for
candidate, and should not be expected to. Costs are real: the published run made
120 model calls and took just under three hours of Actions time.

### Where to put your API key

Never commit a key, and never paste one into a JSON, YAML, issue or workflow
input. No credential is included in this repository.

**In GitHub Actions** (recommended), add a repository secret under
*Settings → Secrets and variables → Actions → New repository secret*:

- `OPENAI_API_KEY` — required for the default configuration
- `ANTHROPIC_API_KEY` — only if you switch a provider to `anthropic`

The workflow reads them via `${{ secrets.* }}` and fails early with an
explanatory message if a required one is missing.

**Locally**, export it into the environment:

```bash
export OPENAI_API_KEY='...'
```

### Run in GitHub Actions

Open *Actions → Run EMSE Study 2 v3.1 (replacement edits) → Run workflow*.

Run the **pilot** first. It is 24 candidates (12 tasks × 2 models × 1
repetition) and exists to confirm the harness works before spending a full run:

| Input | Value |
|---|---|
| Run type | `pilot` |
| Model A provider | `openai` |
| Model A | `gpt-5.6-sol` |
| Model B provider | `openai` |
| Model B | `gpt-5.6-terra` |

Before going further, check in the pilot artifact that `gate-summary.csv` shows
no systematic `indeterminate` column — particularly for CodeQL, where a tooling
failure silently depresses measured sensitivity rather than raising an error.

Then re-run with **Run type: `full`** for the 120-candidate configuration
(12 × 2 × 5). A unique experiment ID is derived from the run ID, attempt number
and commit SHA, so a pilot and a full run can never be merged by accident.

### Run locally

```bash
cp config/experiment.example.json config/experiment.json
```

Set pinned model IDs in `config/experiment.json` — use exact versioned
identifiers, never moving aliases. `config/experiment.json` is gitignored.

```bash
# Confirm the study matrix before spending anything: expect 120 candidates.
dotnet run --project src/ExperimentRunner -- plan config/experiment.json

dotnet run -c Release --project src/ExperimentRunner -- run       config/experiment.json
dotnet run -c Release --project src/ExperimentRunner -- aggregate config/experiment.json
dotnet run -c Release --project src/ExperimentRunner -- blind-review config/experiment.json
```

`scripts/preflight.py` validates the benchmark structure and, importantly,
fails if hidden-oracle or gate code is reachable from the model-visible
repository:

```bash
python3 scripts/preflight.py --config config/experiment.example.json
```

The runner is restartable. A candidate that already has a `result.json` is
skipped, so an interrupted run resumes without regenerating completed work.

## Where output is written

A run writes to `results/<experimentId>/`:

```
results/<experimentId>/
  plan.json                        the full candidate matrix
  candidate-results.csv            one row per candidate
  model-summary.csv                per-model rates
  task-summary.csv                 per-task and per-CWE outcomes
  gate-summary.csv                 per-gate outcomes and latency
  detection-summary.csv            detection performance, all candidates
  verification-only-summary.csv    detection performance, applied edits only
  leave-one-gate-out.csv           ablation
  first-flagged-gate.csv           which gate first rejected or escalated
  paper-table-study2.md            manuscript table
  review-blinded.csv               blinded reviewer pack
  review-private-map.csv           blind-ID mapping; withhold until adjudication
  run-failures.txt                 present only if a candidate crashed
  candidates/<task>__<model>__rNN/
    prompt.txt                     exact prompt sent
    raw-model-output.txt           model's unmodified reply
    raw-provider-response.txt      full provider response, with request ID
    candidate.edits.json           structured replacement plan
    candidate.diff                 diff derived by the verifier
    result.json                    per-gate outcomes, timings, oracle results
    codeql.sarif                   CodeQL findings
    workspace/                     the applied checkout (gitignored: bulk)
    codeql-db/                     CodeQL database (gitignored: ~5 GB per run)
```

In Actions, this directory is uploaded as a single artifact named
`emse-<run_type>-<experimentId>`, with `run-metadata/` holding the exact
configuration and captured tool versions.

Only `results/final-v3.1/` is tracked in git; any other `results/` subdirectory
is treated as local output.

## Inspecting one candidate end to end

Pick any candidate directory and read it in pipeline order. Using the
open-redirect finding as the worked example:

```bash
cd results/final-v3.1/candidates/T11-open-redirect__model-a__r01
```

| Step | File | What to look for |
|---|---|---|
| 1 | `prompt.txt` | Exactly what the model saw. Confirm no hidden oracle appears. |
| 2 | `raw-model-output.txt` | The unmodified reply. |
| 3 | `raw-provider-response.txt` | Provider metadata, including the request ID. |
| 4 | `candidate.edits.json` | The replacement plan: which files, what contents. |
| 5 | `candidate.diff` | The verifier's own diff — the real change. |
| 6 | `result.json` | Per-gate outcomes, then the oracle results. |
| 7 | `codeql.sarif` | Static-analysis findings, if any. |

In `result.json`, read the `gates` array in order. For this candidate every
automatic gate passes and `automaticDecision` is `Pass`, then `oracle-security`
fails. That is a false negative, and the same pattern holds for all ten
open-redirect candidates.

To confirm the boundary between the gate test and the hold-out oracle for this
task, compare `benchmarks/T11-open-redirect/gate-security-tests/Program.cs`
against `benchmarks/T11-open-redirect/hidden-security-tests/Program.cs`. The
gate encodes one exploit case; the oracle encodes the general invariant.

For the contrasting case — the only automatic rejection in the study — see
`T10-zip-slip__model-a__r02`, where the build gate rejects.

## Security note

This repository contains no credentials. Reproduction requires you to supply
your own API key through GitHub Actions Secrets or your local environment. The
benchmark tasks intentionally contain vulnerable code and strings such as
`password=` as test fixtures; they are inert and are the subject of the study,
not a leak.
