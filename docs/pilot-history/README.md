# Pilot and protocol history

**Nothing in this directory is part of the final Study 2 dataset.**

The frozen dataset used by the manuscript is
[`results/final-v3.1/`](../../results/final-v3.1/). These files are retained only
because they document how the protocol reached its final form, which is needed
to explain why the final design differs from the one described in early drafts.

The documents are development notes written at the time of each change. They are
preserved verbatim rather than rewritten, so their wording is operational rather
than academic, and some instructions in them (for example "replace the contents
of your repository with this version") describe the authoring workflow at that
moment, not how to use this replication package. For current instructions see
[`docs/reproduction.md`](../reproduction.md).

| File | Stage | What changed |
|---|---|---|
| `UPDATE-V2-codeql-fix.txt` | v2 | CodeQL C# database creation forced a real rebuild, fixing pilot runs where every CodeQL gate returned `Indeterminate`. |
| `UPDATE-V3-replacement-edits.txt` | v3 | Generation moved from model-authored unified diffs to structured replacement-file edits, removing diff formatting as a confound. |
| `UPDATE-V3.1-reporting.txt` | v3.1 | Reporting separated `Reject` from `Escalate` so that mandatory-review outcomes are not counted as automatic defect detections. This is the frozen protocol. |
| `START-HERE.txt` | v3 | Operational quick-start notes written while the v3 protocol was being validated. |

## Why the earlier runs are not reported as results

Runs before v3.1 were used to validate the harness, not to measure the research
outcome. They are excluded from the manuscript for two reasons:

1. **v2 and earlier used unified diffs.** Candidates could fail before any
   security verification simply because the generated patch would not apply, so
   the measurement conflated diff-formatting reliability with the verification
   stack's ability to detect insecure repairs.
2. **v3 predates the reporting correction.** v3 collapsed `Reject` and
   `Escalate` into a single "flagged" class, which overstates automatic defect
   detection.

The v3.1 protocol was frozen before the final 120-candidate run, and no task,
prompt, gate, threshold, oracle or model configuration was altered after the
final results were observed. See [`docs/methodology.md`](../methodology.md).
