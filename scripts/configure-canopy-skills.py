#!/usr/bin/env python3
"""Preview or register this checkout's factory skills in machine-local settings.

Default: preview only. --apply updates appsettings.Local.json, never project
policies, models, jobs or adoption decisions. Existing unrelated settings survive.
"""
import argparse
import copy
import json
import os
from pathlib import Path
import stat
import tempfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE = 'Canopy factory skills'
NAMES = ('canopy-calibrator', 'canopy-qa', 'canopy-review')


def real_path(path):
    path = Path(os.path.abspath(path))
    if any(part.is_symlink() for part in (path, *path.parents)):
        raise ValueError('Use real paths without symbolic links.')
    return path


def key(obj, name):
    if not isinstance(obj, dict):
        raise ValueError(f'{name} requires an object owner.')
    return next((k for k in obj if k.casefold() == name.casefold()), name)


def unique_keys(pairs):
    result = {}
    seen = set()
    for name, value in pairs:
        if name.casefold() in seen:
            raise ValueError('Configuration contains duplicate case-insensitive keys.')
        seen.add(name.casefold())
        result[name] = value
    return result


def prepare(document, repository=ROOT):
    result = copy.deepcopy(document)
    factory = result.setdefault(key(result, 'Factory'), {})
    entries = factory.setdefault(key(factory, 'Skills'), [])
    if not isinstance(entries, list) or any(not isinstance(e, dict) for e in entries):
        raise ValueError('Factory:Skills must be an array of skill mappings.')
    expected = []
    for name in NAMES:
        file = real_path(Path(repository) / '.agents' / 'skills' / name / 'SKILL.md')
        if not file.is_file() or not file.read_text(encoding='utf-8').strip():
            raise ValueError(f'The existing project skill {name} is unavailable.')
        entry = {'Id': name, 'Source': SOURCE, 'Path': str(file)}
        matches = [e for e in entries if e.get(key(e, 'Id')) == name and e.get(key(e, 'Source')) == SOURCE]
        if len(matches) > 1 or matches and matches[0].get(key(matches[0], 'Path')) != str(file):
            raise ValueError(f'Existing mapping for {name} conflicts; inspect it before changing authority.')
        if not matches:
            entries.append(entry)
        expected.append(entry)
    calibration = factory.setdefault(key(factory, 'Calibration'), {})
    roots_key = key(calibration, 'EditableRoots')
    existing = calibration.get(roots_key, [])
    roots = [str(Path(e['Path']).parent) for e in expected if e['Id'] != 'canopy-calibrator']
    if not isinstance(existing, list) or any(not isinstance(x, str) for x in existing):
        raise ValueError('Factory:Calibration:EditableRoots must be an array of paths.')
    if existing and sorted(existing) != sorted(roots):
        raise ValueError('Existing edit roots differ; inspect them explicitly rather than widening or replacing authority.')
    calibration[roots_key] = roots
    return result, {'skills': expected, 'editableRoots': roots, 'projectPolicies': 'Unchanged; configure a manual project/model explicitly with autoRun and autoApply false.'}


def configure(config, apply=False, repository=ROOT):
    config = real_path(config)
    if config.name != 'appsettings.Local.json' or not config.parent.is_dir():
        raise ValueError('Choose appsettings.Local.json in an existing directory.')
    before = config.read_bytes() if config.exists() else None
    document = json.loads(before, object_pairs_hook=unique_keys) if before is not None else {}
    result, plan = prepare(document, repository)
    plan['changed'] = result != document
    plan['applied'] = False
    if not apply or not plan['changed']:
        return plan
    mode = stat.S_IMODE(config.stat().st_mode) if before is not None else 0o600
    descriptor, temporary = tempfile.mkstemp(prefix='.canopy-skills-', suffix='.tmp', dir=config.parent)
    try:
        with os.fdopen(descriptor, 'w', encoding='utf-8') as output:
            output.write(json.dumps(result, indent=2) + '\n')
        os.chmod(temporary, mode)
        real_path(config)
        observed = config.read_bytes() if config.exists() else None
        if observed != before:
            raise ValueError('Configuration changed during preparation; inspect and retry.')
        os.replace(temporary, config)
        plan['applied'] = True
    finally:
        Path(temporary).unlink(missing_ok=True)
    return plan


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, default=ROOT / 'server/Planner.Api/appsettings.Local.json')
    parser.add_argument('--apply', action='store_true', help='Apply the previewed registration; restart the backend afterward.')
    args = parser.parse_args()
    try:
        print(json.dumps(configure(args.config, args.apply), indent=2))
    except (ValueError, OSError) as error:
        parser.exit(1, f'Cannot register Canopy skills: {error}\n')
