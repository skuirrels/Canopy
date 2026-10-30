#!/usr/bin/env python3
"""Run native API checks against disposable databases, never the live workspace.

Prerequisites: a Debug backend build, Python, Git and Postman CLI 1.75.0.
Provider regressions use local protocol fixtures; no model or GitHub calls.
"""
import json
import argparse
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / 'server/Planner.Api'


def run(*args, env=None):
    print('Running: ' + ' '.join(map(str, args)), flush=True)
    subprocess.run(list(map(str, args)), cwd=ROOT, env=env, check=True)


def workspace_regression():
    with tempfile.TemporaryDirectory(prefix='canopy-ci-') as directory:
        scratch = Path(directory).resolve()
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0))
            base = f'http://127.0.0.1:{sock.getsockname()[1]}'
        env = os.environ | {
            'Database__Path': str(scratch / 'workspace.duckdb'),
            'Agents__WorkspaceRoot': str(scratch / 'worktrees'),
            'PLANNER_URL': base,
        }
        with (scratch / 'api.log').open('w+') as log:
            process = subprocess.Popen(
                ['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base],
                cwd=API, env=env, stdout=log, stderr=log,
            )
            try:
                deadline = time.monotonic() + 30
                while True:
                    if process.poll() is not None:
                        raise RuntimeError('Isolated backend exited during startup.')
                    try:
                        with urllib.request.urlopen(base + '/api/health', timeout=1) as response:
                            health = json.load(response)
                        if health['status'] == 'ready':
                            break
                    except OSError:
                        pass
                    if time.monotonic() >= deadline:
                        raise TimeoutError('Isolated backend did not become ready.')
                    time.sleep(.1)
                run(sys.executable, ROOT / 'scripts/api-regression.py', env=env)
            except BaseException:
                log.flush()
                log.seek(0)
                print(log.read(), flush=True)
                raise
            finally:
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=30)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--suite', choices=('all', 'core', 'providers'), default='all')
    suite = parser.parse_args().suite
    if suite in ('all', 'core'):
        run('dotnet', 'run', '--project', ROOT / 'tests/Planner.StoreRegression', '--',
            API / 'factory-templates.json')
        workspace_regression()
    core = (
        'spec-file', 'agent', 'factory', 'factory-run', 'factory-execution',
        'factory-quality', 'factory-review', 'factory-repair',
        'factory-skills',
    )
    providers = ('factory-delivery', 'factory-intake')
    if suite in ('all', 'providers'):
        run(sys.executable, ROOT / 'scripts/regression-processes-test.py')
    selected = core if suite == 'core' else providers if suite == 'providers' else core + providers
    for name in selected:
        run(sys.executable, ROOT / f'scripts/{name}-regression.py')
    for name in (() if suite == 'core' else ('Planner API', 'Planner regression', 'Factory regression',
                 'Factory review regression', 'Factory repair regression',
                 'Factory delivery regression', 'Factory intake regression',
                 'Factory skills regression')):
        run('postman', 'collection', 'lint', ROOT / 'postman/collections' / name,
            '--reporter', 'cli', '--fail-severity', 'error')
