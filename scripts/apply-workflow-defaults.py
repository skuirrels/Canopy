#!/usr/bin/env python3
"""Apply configured workflow defaults through the public API, preserving tasks.

Defaults come from appsettings.json. Legacy merges are explicit --map inputs.
Dry-run is the default; --apply requires an external workspace snapshot path.
Custom columns are retained after the configured columns.
"""
import argparse
import json
from pathlib import Path
import urllib.request


def apply_defaults(url, config, mappings, project_ids, snapshot=None):
    if snapshot is not None and Path(snapshot).resolve().is_relative_to(Path(__file__).resolve().parents[1]):
        raise ValueError('Save the recovery snapshot outside the source repository.')
    defaults = json.loads(Path(config).read_text())['WorkflowDefaults']
    configured = {stage['Name'].casefold(): stage for stage in defaults}
    if not defaults or len(configured) != len(defaults) or any(not s['Name'].strip() for s in defaults):
        raise ValueError('Configure distinct, nonempty workflow defaults.')
    if any(target.casefold() not in configured for target in mappings.values()):
        raise ValueError('Every mapping destination must be a configured default.')
    if any(source.casefold() in configured for source in mappings):
        raise ValueError('A configured default cannot also be a legacy merge source.')

    def request(path, method='GET', body=None):
        req = urllib.request.Request(url.rstrip('/') + path, method=method,
            data=None if body is None else json.dumps(body).encode(),
            headers={'Content-Type': 'application/json'})
        with urllib.request.urlopen(req, timeout=30) as response:
            return json.load(response)

    state = request('/api/workspace')
    selected = [p for p in state['projects'] if not project_ids or p['id'] in project_ids]
    if project_ids and set(project_ids) != {p['id'] for p in selected}:
        raise ValueError('A selected project does not exist.')
    # Validate every project before making any changes.
    for project in selected:
        stages = [s for s in state['statuses'] if s['projectId'] == project['id']]
        if len({s['name'].casefold() for s in stages}) != len(stages):
            raise ValueError('Duplicate workflow names must be resolved before applying defaults.')
        for stage in stages:
            destination = mappings.get(stage['name'].casefold())
            if destination and stage['isCompleted'] != configured[destination.casefold()]['IsCompleted']:
                raise ValueError('A legacy merge must preserve completion semantics.')
    report = {'projects': len(selected), 'defaults': [s['Name'] for s in defaults],
              'legacyMerges': mappings, 'applied': snapshot is not None}
    if snapshot is None:
        return report
    # Exclusive creation avoids replacing a previous recovery snapshot.
    with Path(snapshot).open('x') as file:
        json.dump(state, file, indent=2)

    def mutate(project_id, path, method, body=None):
        nonlocal state
        revision = next(p['revision'] for p in state['projects'] if p['id'] == project_id)
        if method == 'DELETE':
            path += '?expectedRevision=' + str(revision)
        else:
            body = {**body, 'expectedRevision': revision}
        state = request(path, method, body)

    moved = 0
    for project in selected:
        pid = project['id']
        def stages():
            return sorted([s for s in state['statuses'] if s['projectId'] == pid], key=lambda s: s['position'])
        desired_ids = []
        for target in defaults:
            current = next((s for s in stages() if s['name'].casefold() == target['Name'].casefold()), None)
            if current is None:
                previous = {s['id'] for s in stages()}
                mutate(pid, f'/api/projects/{pid}/statuses', 'POST', {'name': target['Name'], 'color': target['Color']})
                current = next(s for s in stages() if s['id'] not in previous)
            if (current['name'], current['color'], current['isCompleted']) != (target['Name'], target['Color'], target['IsCompleted']):
                mutate(pid, f"/api/statuses/{current['id']}", 'PATCH', {'name': target['Name'], 'color': target['Color'], 'isCompleted': target['IsCompleted']})
            desired_ids.append(current['id'])
        for source in list(stages()):
            destination = mappings.get(source['name'].casefold())
            if not destination:
                continue
            target = next(s for s in stages() if s['name'].casefold() == destination.casefold())
            affected = sorted([i for i in state['items'] if i['statusId'] == source['id']], key=lambda i: (i['position'], i['createdAt'], i['id']))
            for item in affected:
                mutate(pid, f"/api/items/{item['id']}/move", 'POST', {'parentId': item['parentId'], 'statusId': target['id'], 'beforeId': None})
                moved += 1
            mutate(pid, f"/api/statuses/{source['id']}", 'DELETE')
        ordered = desired_ids + [s['id'] for s in stages() if s['id'] not in desired_ids]
        if [s['id'] for s in stages()] != ordered:
            mutate(pid, f'/api/projects/{pid}/status-order', 'POST', {'statusIds': ordered})
    return {**report, 'tasksMoved': moved}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', required=True)
    parser.add_argument('--config', type=Path, default=Path(__file__).resolve().parents[1] / 'server/Planner.Api/appsettings.json')
    parser.add_argument('--map', action='append', default=[], metavar='OLD=NEW')
    scope = parser.add_mutually_exclusive_group(required=True)
    scope.add_argument('--all-projects', action='store_true')
    scope.add_argument('--project-id', action='append')
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--snapshot', type=Path)
    args = parser.parse_args()
    if args.apply and not args.snapshot:
        parser.error('--apply requires --snapshot outside the source repository')
    mappings = {}
    for value in args.map:
        source, destination = value.split('=', 1)
        mappings[source.strip().casefold()] = destination.strip()
    print(json.dumps(apply_defaults(args.url, args.config, mappings, args.project_id,
                                   args.snapshot if args.apply else None), indent=2))


if __name__ == '__main__':
    main()
