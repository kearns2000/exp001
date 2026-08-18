# Study 2 protocol

## Objective

Measure whether a verification-gated .NET CI/CD pipeline can identify incorrect or insecure LLM-generated security patches, and quantify the contribution and operational cost of its gates.

## Primary outcome

Joint functional-and-security correctness of each generated patch.

## Research questions

- **RQ1:** What proportion of LLM-generated security patches are functionally correct, security-correct, and jointly correct?
- **RQ2:** How effectively does the complete automatic gate stack reject candidates that fail the executable oracle?
- **RQ3:** Which gates contribute distinct detections, and what changes under leave-one-gate-out ablation?
- **RQ4:** What classes of defects survive automatic verification and require repository-specific invariants or human judgement?
- **RQ5:** What latency and review-load cost is introduced by each verification layer?

## Sampling

Twelve purpose-built .NET 10 tasks cover distinct security properties. The confirmatory run uses ten independent generations per task, distributed across the pinned model configurations in `config/experiment.json`. The default two-model design uses five generations per model per task, yielding 120 candidates.

## Independence and stochasticity

Each candidate starts from the same task baseline commit. No generated patch or reviewer judgement is fed into another generation. Repetitions are recorded explicitly. If a provider exposes a seed or request identifier, record it in the archived raw provider response or experiment metadata.

## Blinding

Generate `review-blinded.csv` after automatic evaluation. Reviewers should receive the blind ID, task issue/security property, and candidate diff without model identity or automatic decision. Keep `review-private-map.csv` separate until adjudication is complete.

## Reviewer labels

Use: `correct`, `security-defect`, `functional-regression`, `both-defective`, `uncertain`. Two reviewers independently label a stratified sample; disagreements are adjudicated against written security invariants and executable evidence.

## Exclusions

Exclude only infrastructure-invalid attempts (provider outage, corrupted workspace, unavailable SDK/tool) and rerun them under the same experiment version. A model response that does not contain an applicable diff is a model outcome, not an infrastructure exclusion.

## Pilot rule

Run one candidate per task before the confirmatory experiment. Use the pilot only to validate task buildability, prompt formatting, hidden-test execution and result capture. Pilot candidates are not included in final estimates.

## Analysis

Report counts and proportions by model and CWE/task. Compute gate-level detection, first-rejecting-gate distribution, leave-one-gate-out detection, escalation rate, median/P95 latency, and reviewer agreement. Do not present the purpose-built benchmark proportion as an estimate of the population defect rate of any model.
