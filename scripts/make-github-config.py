#!/usr/bin/env python3
import json, os, sys

if len(sys.argv) != 8:
    raise SystemExit("usage: make-github-config.py <pilot|full> <providerA> <modelA> <providerB> <modelB> <codeqlPath> <output>")
mode, provider_a, model_a, provider_b, model_b, codeql, output = sys.argv[1:]
if mode not in {"pilot", "full"}:
    raise SystemExit("mode must be pilot or full")
for p in (provider_a, provider_b):
    if p not in {"openai", "anthropic"}:
        raise SystemExit(f"unsupported provider: {p}")
reps = 1 if mode == "pilot" else 5
parallelism = 1 if mode == "pilot" else 2
run_id = os.environ.get("GITHUB_RUN_ID", "local")
attempt = os.environ.get("GITHUB_RUN_ATTEMPT", "1")
sha = os.environ.get("GITHUB_SHA", "unknown")[:12]
experiment_id = f"emse-study2-{mode}-gh{run_id}-a{attempt}-{sha}"

def model(slot, provider, model):
    return {
        "id": slot,
        "provider": provider,
        "model": model,
        "repetitions": reps,
        "apiKeyEnvironmentVariable": "OPENAI_API_KEY" if provider == "openai" else "ANTHROPIC_API_KEY",
        "maxOutputTokens": 8000
    }

config = {
    "experimentId": experiment_id,
    "maxParallelism": parallelism,
    "timeoutMinutes": 15,
    "benchmarkRoot": "benchmarks",
    "resultsRoot": "results",
    "runCodeQl": True,
    "codeQlExecutable": codeql,
    "codeQlSuite": "codeql/csharp-queries:codeql-suites/csharp-security-extended.qls",
    "models": [model("model-a", provider_a, model_a), model("model-b", provider_b, model_b)]
}
with open(output, "w", encoding="utf-8") as f:
    json.dump(config, f, indent=2)
    f.write("\n")
print(experiment_id)
