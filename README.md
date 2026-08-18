# EMSE SECUTE 2026 — LLM Security Patch Verification Experiment

Runnable companion experiment for the manuscript **Verification-Gated LLM Patch Generation: An Empirical Evaluation of Security Controls in .NET CI/CD Pipelines**.

## Design

Study 2 uses **12 .NET 10 security-repair tasks × 10 independent generations = 120 candidate patches** with the supplied example configuration (two model slots, five repetitions each). Each task contains a visible repository and public regression checks plus a separate hidden security oracle that is never included in the generation prompt.

The candidate pipeline is:

1. model generates a unified diff only;
2. `git apply --whitespace=error`;
3. repository policy / diff-scope gate;
4. `dotnet restore --locked-mode`;
5. `dotnet build -c Release`;
6. visible regression checks;
7. NuGet audit (`NU1903`/`NU1904` are rejection evidence);
8. a hidden **gate security-proof test** containing one known exploit case;
9. optional CodeQL C# security-extended analysis;
10. **after the automatic decision**, separate hidden functional and hold-out security oracles are executed solely to establish ground truth;
11. blinded human review export.

An automatic pass is **not** interpreted as approval. The primary outcome is **joint functional-and-security correctness**, determined by hidden executable oracles and then available for blinded reviewer adjudication. The hidden oracles are never counted as automatic gates; doing so would leak the answer key into the detector being evaluated.

## Requirements

- .NET 10 SDK
- Git
- CodeQL CLI (optional but recommended for the paper run)
- API credentials for configured providers, or a local command provider

.NET 10 audits direct and transitive NuGet dependencies by default. Keep SDK/tool versions pinned and record `dotnet --info`, `git --version`, and `codeql version` with each final study run.

## Configure

```bash
cp config/experiment.example.json config/experiment.json
```

Set real, pinned provider model IDs in `config/experiment.json`; do not use moving aliases for the final paper run.

```bash
export OPENAI_API_KEY='...'
export ANTHROPIC_API_KEY='...'
```

## Check the study matrix

```bash
dotnet run --project src/ExperimentRunner -- plan config/experiment.json
```

With the example config this should report 120 candidates.

## Run

```bash
dotnet run -c Release --project src/ExperimentRunner -- run config/experiment.json
```

The runner is restartable. A candidate with an existing `result.json` is skipped, so an interrupted run can be resumed without regenerating completed candidates.

## Aggregate

```bash
dotnet run -c Release --project src/ExperimentRunner -- aggregate config/experiment.json
dotnet run -c Release --project src/ExperimentRunner -- blind-review config/experiment.json
```

Outputs include:

- `candidate-results.csv`
- `model-summary.csv`
- `gate-summary.csv`
- `paper-table-study2.md`
- `review-blinded.csv`
- `review-private-map.csv`
- per-candidate prompt, raw output, exact diff, gate evidence and result JSON

## Important research controls

Do not inspect hidden tests while manually prompting a model. Do not change prompts, task text, gate thresholds, SDK version or model aliases after the first recorded candidate. If a pilot reveals a harness defect, fix it, increment the experiment ID and discard the pilot from confirmatory analysis.

The benchmark is purpose-built and therefore complements rather than replaces evaluation on production repositories. Report that limitation explicitly.

---

## Easiest route: run the experiment entirely in GitHub Actions

This repository includes `.github/workflows/run-experiment.yml`. You do **not** need .NET or CodeQL installed on your computer.

### 1. Create a private GitHub repository

Create an empty private repository, extract this ZIP, and upload/push **the contents of this folder** so that `EMSE.SecurityExperiment.sln` is at the repository root. Do not upload the outer ZIP as a single file.

### 2. Add API credentials as GitHub Actions secrets

In the GitHub repository open:

**Settings → Secrets and variables → Actions → New repository secret**

For the default configuration add:

- `OPENAI_API_KEY` — your OpenAI API key

Only add `ANTHROPIC_API_KEY` if you change either workflow model provider to `anthropic`.

Never paste an API key into JSON, YAML, source control, an issue, or an Actions input.

### 3. Run the pilot first

Open:

**Actions → Run EMSE security experiment → Run workflow**

Leave these defaults initially:

- Run type: `pilot`
- Model A provider: `openai`
- Model A: `gpt-5.6-sol`
- Model B provider: `openai`
- Model B: `gpt-5.6-terra`

Then click **Run workflow**.

The pilot runs **24 candidates**: 12 benchmark tasks × 2 models × 1 generation. Pilot results validate the harness only and should not be mixed into the confirmatory paper results.

### 4. Download the pilot artifact

When the workflow finishes, open the workflow run and download the artifact named approximately:

`emse-pilot-emse-study2-pilot-...`

It contains the exact prompts, provider responses, generated diffs, gate evidence, hidden-oracle outcomes, CSV summaries, blinded-review pack, environment capture, workflow log, and SHA-256 manifest.

Check `run-failures.txt` if the workflow is marked failed. A failed workflow still uploads the research artifact whenever possible.

### 5. Run the confirmatory experiment

After the pilot is clean, run the workflow again and change **Run type** to `full`.

The full configuration runs **120 fresh candidates**: 12 tasks × 2 models × 5 independent generations. A unique experiment ID is generated from the GitHub run ID, attempt, and commit SHA so pilot and final data cannot be accidentally merged.

### Reproducibility pins

The GitHub workflow currently pins:

- .NET SDK `10.0.111`
- CodeQL CLI/bundle `2.26.3`
- CodeQL Linux bundle SHA-256 `77e5be1b550d66662e600e795b6cf2ea1729e853e3dc79e02594f767039d2a29`

The selected provider/model IDs, Git commit SHA, GitHub run ID, .NET information, Git version and CodeQL version are written into the result artifact under `run-metadata/`.

The workflow defaults to OpenAI model IDs `gpt-5.6-sol` and `gpt-5.6-terra`. The workflow form lets you replace either model or change either provider to Anthropic without editing source files. Record the exact IDs used for the confirmatory run in the manuscript.
