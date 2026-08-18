#!/usr/bin/env python3
from pathlib import Path
import argparse, json, shutil, sys

parser=argparse.ArgumentParser()
parser.add_argument('--config', default='config/experiment.example.json')
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
errors=[]

try:
    cfg=json.loads((root/args.config).read_text())
except Exception as e:
    print(f'Config error: {e}', file=sys.stderr); sys.exit(1)

tasks=[]
for d in sorted((root/'benchmarks').iterdir()):
    if not d.is_dir(): continue
    try: task=json.loads((d/'task.json').read_text())
    except Exception as e: errors.append(f'{d.name}: invalid task.json: {e}'); continue
    tasks.append(task)
    required=[
        d/'repo'/'Task.sln', d/'repo'/'Target'/'Target.csproj', d/'repo'/'PublicTests'/'PublicTests.csproj',
        d/'gate-security-tests'/'GateSecurity.csproj',
        d/'hidden-functional-tests'/'OracleFunctional.csproj',
        d/'hidden-security-tests'/'OracleSecurity.csproj'
    ]
    for p in required:
        if not p.exists(): errors.append(f'missing {p.relative_to(root)}')
    repo_text='\n'.join(p.read_text(errors='ignore') for p in (d/'repo').rglob('*') if p.is_file())
    if 'OracleFunctional' in repo_text or 'OracleSecurity' in repo_text or 'GateSecurity' in repo_text:
        errors.append(f'{d.name}: hidden/gate oracle leaked into model-visible repo')

candidate_count=len(tasks)*sum(int(m.get('repetitions',1)) for m in cfg.get('models',[]))
print(f'tasks={len(tasks)}')
print(f'model_slots={len(cfg.get("models",[]))}')
print(f'candidates={candidate_count}')
print(f'dotnet={shutil.which("dotnet") or "NOT FOUND"}')
print(f'git={shutil.which("git") or "NOT FOUND"}')
print(f'codeql={shutil.which("codeql") or "NOT FOUND"}')

if len(tasks)!=12: errors.append(f'expected 12 tasks, found {len(tasks)}')
if Path(args.config).name=='experiment.example.json' and candidate_count!=120:
    errors.append(f'expected 120 candidates for example config, found {candidate_count}')
if errors:
    print('\nPRE-FLIGHT FAILED', file=sys.stderr)
    for e in errors: print(' - '+e, file=sys.stderr)
    sys.exit(1)
print('PRE-FLIGHT STRUCTURE PASSED')
