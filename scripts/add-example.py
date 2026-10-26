#!/usr/bin/env python3
"""Explicitly create fictional example data through the same public API as the UI.

Never run on startup. Refuses to overwrite an existing project with the same name.
"""
import argparse
from datetime import datetime, date, timedelta
import json
from pathlib import Path
import urllib.request
from zoneinfo import ZoneInfo

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--base-url', required=True, help='Explicit URL of the planner instance to populate')
parser.add_argument('--anchor-date', type=date.fromisoformat, default=datetime.now(ZoneInfo('Europe/London')).date())
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
fixture = json.loads((root / 'examples/website-launch.json').read_text())

def call(path, method='GET', body=None):
    request = urllib.request.Request(args.base_url.rstrip('/') + path, method=method,
        headers={'Content-Type': 'application/json'}, data=None if body is None else json.dumps(body).encode())
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)

state = call('/api/workspace')
before = state
if any(p['name'] == fixture['name'] for p in state['projects']):
    raise SystemExit('The example project already exists. No data was changed; your edits are preserved.')
keys = {t['key'] for t in fixture['tasks']}
assert len(keys) == len(fixture['tasks']), 'Example keys must be unique.'
for task in fixture['tasks']:
    assert task['parent'] is None or task['parent'] in keys
    assert set(task['dependsOn']) <= keys
    assert task['startOffsetDays'] <= task['dueOffsetDays']
    assert task['assignee'] in fixture['people']

state = call('/api/projects', 'POST', {k: fixture[k] for k in ('name', 'description', 'color')})
project = next(p for p in state['projects'] if p['name'] == fixture['name'])
pid = project['id']
columns = {s['name']: s['id'] for s in state['statuses'] if s['projectId'] == pid}
missing = {t['status'] for t in fixture['tasks']} - columns.keys()
if missing:
    raise SystemExit(f'Configured workflow is missing example statuses: {missing}. Project {pid} is empty; no fallback values were used.')
people = {p['name']: p['id'] for p in state['people']}
for name in fixture['people']:
    if name not in people:
        state = call('/api/people', 'POST', {'name': name})
        people[name] = next(p['id'] for p in state['people'] if p['name'] == name)

def revision():
    return next(p['revision'] for p in state['projects'] if p['id'] == pid)

ids = {}
pending = fixture['tasks'].copy()
while pending:
    ready = [t for t in pending if t['parent'] is None or t['parent'] in ids]
    if not ready:
        raise SystemExit('The example hierarchy contains a cycle.')
    for task in ready:
        previous = {i['id'] for i in state['items']}
        state = call(f'/api/projects/{pid}/items', 'POST', {'expectedRevision': revision(), 'title': task['title'],
            'parentId': ids.get(task['parent']), 'statusId': columns[task['status']]})
        ids[task['key']] = next(i['id'] for i in state['items'] if i['id'] not in previous)
        pending.remove(task)

def paragraph(text):
    return {'type': 'paragraph', 'content': [{'type': 'text', 'text': text}]}

for task in fixture['tasks']:
    spec = {'type': 'doc', 'content': [
        paragraph('Fictional example — edit this freely while exploring the planner.'),
        {'type': 'heading', 'attrs': {'level': 2}, 'content': [{'type': 'text', 'text': 'Outcome'}]},
        paragraph(task['outcome']),
        {'type': 'heading', 'attrs': {'level': 3}, 'content': [{'type': 'text', 'text': 'Acceptance checklist'}]},
        {'type': 'taskList', 'content': [{'type': 'taskItem', 'attrs': {'checked': task['status'] == 'Done'},
            'content': [paragraph(text)]} for text in task['acceptance']]},
        {'type': 'heading', 'attrs': {'level': 3}, 'content': [{'type': 'text', 'text': 'Try it out'}]},
        paragraph('Change the status, add a subitem, move this branch, or follow a dependency to see the same task in both views.')
    ]}
    state = call(f"/api/items/{ids[task['key']]}", 'PATCH', {
        'expectedRevision': revision(), 'title': task['title'], 'specJson': json.dumps(spec),
        'statusId': columns[task['status']], 'priority': task['priority'], 'assigneeId': people[task['assignee']],
        'startDate': (args.anchor_date + timedelta(days=task['startOffsetDays'])).isoformat(),
        'dueDate': (args.anchor_date + timedelta(days=task['dueOffsetDays'])).isoformat(),
        'dependencyIds': [ids[key] for key in task['dependsOn']]})

# Inspect a fresh persisted snapshot and prove existing user data was not changed.
after = call('/api/workspace')
for collection, entries in before.items():
    for entry in entries:
        assert next(e for e in after[collection] if e['id'] == entry['id']) == entry, f'Existing {collection} entry changed.'
items = [i for i in after['items'] if i['projectId'] == pid]
links = [d for d in after['dependencies'] if d['projectId'] == pid]
assert len(items) == len(fixture['tasks'])
for task in fixture['tasks']:
    item = next(i for i in items if i['id'] == ids[task['key']])
    assert item['parentId'] == ids.get(task['parent'])
    assert item['assigneeId'] == people[task['assignee']]
    assert item['startDate'] == (args.anchor_date + timedelta(days=task['startOffsetDays'])).isoformat()
    assert item['dueDate'] == (args.anchor_date + timedelta(days=task['dueOffsetDays'])).isoformat()
    assert item['statusId'] == columns[task['status']]
    assert {d['dependsOnId'] for d in links if d['itemId'] == item['id']} == {ids[k] for k in task['dependsOn']}
    assert json.loads(item['specJson'])['content'][2]['content'][0]['text'] == task['outcome']
print(json.dumps({'projectId': pid, 'name': fixture['name'], 'tasks': len(items), 'rootTasks': sum(i['parentId'] is None for i in items),
    'assignees': len(fixture['people']), 'dependencies': len(links), 'anchorDate': args.anchor_date.isoformat(), 'existingDataPreserved': True}, indent=2))
