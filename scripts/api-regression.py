#!/usr/bin/env python3
"""Real HTTP / DuckDB regression. Creates and removes its own isolated projects."""
import concurrent.futures
import json
import os
import urllib.error
import urllib.request
import uuid

BASE = os.environ.get('PLANNER_URL', 'http://127.0.0.1:5274')
count = 0

def call(path, method='GET', body=None, expected=200):
    request = urllib.request.Request(BASE + path, data=None if body is None else (body if isinstance(body, str) else json.dumps(body)).encode(), headers={'Content-Type': 'application/json'}, method=method)
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            code = response.status
            value = json.load(response)
    except urllib.error.HTTPError as error:
        code = error.code
        value = json.load(error)
    assert code == expected, (path, code, expected, value)
    return value

def check(condition, message):
    global count
    assert condition, message
    count += 1
    print(f'PASS {count:02}: {message}')

created = []
created_people = []
try:
    name = 'API regression ' + uuid.uuid4().hex[:8]
    state = call('/api/projects', 'POST', {'name': name, 'description': 'Isolated regression project; removed by the test.', 'color': '#447755'})
    project = next(p for p in state['projects'] if p['name'] == name)
    pid = project['id']; created.append(pid)
    statuses = [s for s in state['statuses'] if s['projectId'] == pid]
    check(len(statuses) > 0, 'New project gets configured workflow columns')
    ordered = [s['id'] for s in reversed(statuses)]
    state = call(f'/api/projects/{pid}/status-order', 'POST', {'expectedRevision': project['revision'], 'statusIds': ordered})
    check([s['id'] for s in sorted([s for s in state['statuses'] if s['projectId'] == pid], key=lambda s:s['position'])] == ordered, 'Workflow column ordering persists')
    project = next(p for p in state['projects'] if p['id'] == pid)
    call(f'/api/projects/{pid}/status-order', 'POST', {'expectedRevision': project['revision'], 'statusIds': ordered[:-1]}, 400)
    check(True, 'Incomplete workflow reorder is rejected atomically')
    first = statuses[0]['id']; last = statuses[-1]['id']
    def revision():
        return next(p['revision'] for p in state['projects'] if p['id'] == pid)
    def add(title, parent=None):
        global state
        state = call(f'/api/projects/{pid}/items', 'POST', {'expectedRevision': revision(), 'title': title, 'parentId': parent, 'statusId': first})
        return next(i for i in state['items'] if i['projectId'] == pid and i['title'] == title)
    parent = add('Parent'); child = add('Child', parent['id']); grandchild = add('Grandchild', child['id']); sibling = add('Sibling')
    foreign_name = 'Other project ' + uuid.uuid4().hex[:8]
    state = call('/api/projects', 'POST', {'name': foreign_name, 'description': 'Cross-project invariant check', 'color': '#557788'})
    foreign_project = next(p for p in state['projects'] if p['name'] == foreign_name); created.append(foreign_project['id'])
    foreign_status = next(s['id'] for s in state['statuses'] if s['projectId'] == foreign_project['id'])
    state = call(f"/api/projects/{foreign_project['id']}/items", 'POST', {'expectedRevision': 0, 'title': 'Foreign task', 'parentId': None, 'statusId': foreign_status})
    foreign_item = next(i for i in state['items'] if i['projectId'] == foreign_project['id'])
    call(f'/api/projects/{pid}/items', 'POST', {'expectedRevision': revision(), 'title': 'Invalid foreign parent', 'parentId': foreign_item['id'], 'statusId': first}, 404)
    call(f"/api/items/{sibling['id']}/move", 'POST', {'expectedRevision': revision(), 'parentId': None, 'statusId': foreign_status}, 400)
    check(True, 'Cross-project parents and workflow columns are rejected')
    check(grandchild['parentId'] == child['id'], 'Arbitrary nesting persists through public API')
    call(f"/api/items/{parent['id']}/move", 'POST', {'expectedRevision': revision(), 'parentId': grandchild['id'], 'statusId': first}, 400)
    check(next(i for i in call('/api/workspace')['items'] if i['id'] == parent['id'])['parentId'] is None, 'Hierarchy cycle rejected without changing data')
    state = call(f"/api/items/{sibling['id']}/move", 'POST', {'expectedRevision': revision(), 'parentId': None, 'statusId': first, 'beforeId': parent['id']})
    check(next(i for i in state['items'] if i['id'] == sibling['id'])['position'] == 0, 'Reordering commits the requested order')
    def fields(item, deps=[]):
        current = next(i for i in state['items'] if i['id'] == item['id'])
        return {k: current[k] for k in ['title', 'specJson', 'statusId', 'priority', 'assigneeIds', 'estimatedHours', 'startDate', 'dueDate']} | {'expectedRevision': revision(), 'dependencyIds': deps}
    call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling, [foreign_item['id']]), 404)
    check(True, 'Dependencies cannot reference another project')
    person_name = 'Regression assignee ' + uuid.uuid4().hex[:8]
    state = call('/api/people', 'POST', {'name': person_name})
    person = next(p for p in state['people'] if p['name'] == person_name); created_people.append(person['id'])
    legacy = fields(sibling); legacy.pop('assigneeIds')
    state = call(f"/api/items/{sibling['id']}", 'PATCH', legacy | {'assigneeId': person['id']})
    check(next(i for i in state['items'] if i['id'] == sibling['id'])['assigneeId'] == person['id'], 'Assignees persist through task updates')
    second_name = 'Second regression assignee ' + uuid.uuid4().hex[:8]
    state = call('/api/people', 'POST', {'name': second_name})
    second = next(p for p in state['people'] if p['name'] == second_name); created_people.append(second['id'])
    both = [person['id'], second['id']]
    state = call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling) | {'assigneeIds': both})
    assigned = next(i for i in call('/api/workspace')['items'] if i['id'] == sibling['id'])
    check(assigned['assigneeIds'] == both and assigned['assigneeId'] == person['id'], 'Two assignees persist with legacy first-assignee compatibility')
    unchanged_assignments = call('/api/workspace')
    for invalid in [[person['id'], person['id']], [person['id'], str(uuid.uuid4())]]:
        call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling) | {'assigneeIds': invalid}, 400)
    call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling) | {'assigneeId': second['id']}, 400)
    call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling) | {'expectedRevision': revision() - 1, 'assigneeIds': []}, 409)
    check(call('/api/workspace') == unchanged_assignments, 'Duplicate, missing, conflicting and stale assignee edits reject atomically')
    state = call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling) | {'priority': 'medium'})
    check(next(i for i in state['items'] if i['id'] == sibling['id'])['assigneeIds'] == both, 'Other task edits preserve both assignees')
    state = call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling) | {'assigneeIds': list(reversed(both))})
    check(next(i for i in call('/api/workspace')['items'] if i['id'] == sibling['id'])['assigneeIds'] == list(reversed(both)), 'Assignment order round-trips without duplicate rows')
    state = call(f"/api/people/{person['id']}", 'DELETE')
    check(next(i for i in state['items'] if i['id'] == sibling['id'])['assigneeIds'] == [second['id']], 'Removing one person preserves the other assignment and the task')
    state = call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling) | {'assigneeIds': []})
    check(next(i for i in call('/api/workspace')['items'] if i['id'] == sibling['id'])['assigneeIds'] == [], 'Empty assignee list clears all assignments')
    state = call(f"/api/items/{child['id']}", 'PATCH', fields(child) | {'assigneeIds': [second['id']]})
    update = fields(child, [sibling['id']]) | {'priority': 'high', 'startDate': '2026-10-26', 'dueDate': '2026-11-01', 'specJson': json.dumps({'type': 'doc', 'content': [{'type': 'paragraph', 'content': [{'type': 'text', 'text': 'Persisted spec & details'}]}]})}
    state = call(f"/api/items/{child['id']}", 'PATCH', update)
    saved_child = next(i for i in state['items'] if i['id'] == child['id'])
    check(saved_child['priority'] == 'high' and 'Persisted spec' in saved_child['specJson'] and saved_child['dueDate'] == '2026-11-01', 'Spec, priority and dates round-trip through DuckDB')
    check(saved_child['estimatedHours'] is None, 'New tasks have no estimate until explicitly entered')
    state = call(f"/api/items/{child['id']}", 'PATCH', fields(child, [sibling['id']]) | {'estimatedHours': 2.5})
    estimated_child = next(i for i in state['items'] if i['id'] == child['id'])
    check(estimated_child['estimatedHours'] == 2.5 and estimated_child['startDate'] == saved_child['startDate'] and estimated_child['dueDate'] == saved_child['dueDate'], 'Fractional effort saves without changing task dates')
    state = call(f"/api/items/{child['id']}", 'PATCH', fields(child, [sibling['id']]) | {'startDate': '2026-10-25', 'dueDate': '2026-11-10'})
    check(next(i for i in state['items'] if i['id'] == child['id'])['estimatedHours'] == 2.5, 'Changing calendar dates does not recalculate effort')
    omitted_estimate = fields(child, [sibling['id']]); omitted_estimate.pop('estimatedHours')
    state = call(f"/api/items/{child['id']}", 'PATCH', omitted_estimate | {'startDate': saved_child['startDate'], 'dueDate': saved_child['dueDate']})
    check(next(i for i in state['items'] if i['id'] == child['id'])['estimatedHours'] == 2.5, 'Legacy callers that omit the estimate preserve it')
    estimate_before = call('/api/workspace')
    call(f"/api/items/{child['id']}", 'PATCH', fields(child, [sibling['id']]) | {'estimatedHours': -1}, 400)
    overflow = fields(child, [sibling['id']]); overflow.pop('estimatedHours')
    call(f"/api/items/{child['id']}", 'PATCH', json.dumps(overflow)[:-1] + ', \"estimatedHours\": 1e999}', 400)
    check(call('/api/workspace') == estimate_before, 'Negative and non-finite effort reject without changing dates or other fields')
    state = call(f"/api/items/{child['id']}", 'PATCH', fields(child, [sibling['id']]) | {'estimatedHours': 0})
    check(next(i for i in call('/api/workspace')['items'] if i['id'] == child['id'])['estimatedHours'] == 0, 'Zero hours remains distinct from an unknown estimate')
    state = call(f"/api/items/{child['id']}", 'PATCH', fields(child, [sibling['id']]) | {'estimatedHours': None})
    check(next(i for i in call('/api/workspace')['items'] if i['id'] == child['id'])['estimatedHours'] is None, 'Explicit null clears an estimate')
    state = call(f"/api/items/{child['id']}", 'PATCH', fields(child, [sibling['id']]) | {'estimatedHours': 2.5})
    call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling, [child['id']]), 400)
    check(len([d for d in call('/api/workspace')['dependencies'] if d['projectId'] == pid]) == 1, 'Circular dependency rejected atomically')
    call(f"/api/items/{child['id']}", 'PATCH', fields(saved_child, [sibling['id']]) | {'startDate': '2026-12-01', 'dueDate': '2026-11-01'}, 400)
    check(next(i for i in call('/api/workspace')['items'] if i['id'] == child['id'])['startDate'] == '2026-10-26', 'Invalid date range leaves saved values unchanged')
    stale = revision()
    state = call(f"/api/items/{parent['id']}/move", 'POST', {'expectedRevision': stale, 'parentId': None, 'statusId': last})
    call(f"/api/items/{parent['id']}/move", 'POST', {'expectedRevision': stale, 'parentId': None, 'statusId': first}, 409)
    check(next(i for i in call('/api/workspace')['items'] if i['id'] == parent['id'])['statusId'] == last, 'Stale edit returns 409 and cannot overwrite newer changes')
    check(next(i for i in state['items'] if i['id'] == child['id'])['parentId'] == parent['id'], 'Moving a parent preserves its subtree and independent child status')
    state = call(f'/api/projects/{pid}/layout', 'PUT', {'expectedRevision': revision(), 'positions': [{'id': child['id'], 'x': 321.5, 'y': -42.25}]})
    check(next(i for i in call('/api/workspace')['items'] if i['id'] == child['id'])['x'] == 321.5, 'Map position persists independently of hierarchy')
    old_items = {i['id']: {k: v for k, v in i.items() if k != 'siblingPosition'} for i in state['items']}
    state = call(f'/api/projects/{pid}/sibling-order', 'POST', {'expectedRevision': revision(), 'parentId': None, 'itemIds': [sibling['id'], parent['id']]})
    roots = sorted([i for i in state['items'] if i['projectId'] == pid and i['parentId'] is None], key=lambda i: i['siblingPosition'])
    check([i['id'] for i in roots] == [sibling['id'], parent['id']], 'Components in different workflow statuses reorder together')
    check(old_items == {i['id']: {k: v for k, v in i.items() if k != 'siblingPosition'} for i in state['items']}, 'Component reorder preserves all task fields, Kanban positions, map coordinates and descendants')
    unchanged = call('/api/workspace')
    for invalid in [[parent['id']], [parent['id'], parent['id']], [parent['id'], child['id']], [parent['id'], foreign_item['id']]]:
        call(f'/api/projects/{pid}/sibling-order', 'POST', {'expectedRevision': revision(), 'parentId': None, 'itemIds': invalid}, 400)
    call(f'/api/projects/{pid}/sibling-order', 'POST', {'expectedRevision': revision(), 'parentId': foreign_item['id'], 'itemIds': []}, 404)
    call(f'/api/projects/{pid}/sibling-order', 'POST', {'expectedRevision': revision() - 1, 'parentId': None, 'itemIds': [parent['id'], sibling['id']]}, 409)
    check(call('/api/workspace') == unchanged, 'Invalid, duplicate, foreign, non-sibling and stale component orders reject atomically')
    before_sequence = {i['id']: i['siblingPosition'] for i in state['items']}
    state = call(f"/api/items/{parent['id']}/move", 'POST', {'expectedRevision': revision(), 'parentId': None, 'statusId': first, 'beforeId': sibling['id']})
    check(before_sequence == {i['id']: i['siblingPosition'] for i in state['items']}, 'Kanban status and position changes preserve the component sequence')
    state = call(f'/api/projects/{pid}/sibling-order', 'POST', {'expectedRevision': revision(), 'parentId': parent['id'], 'itemIds': [child['id']]})
    check(next(i for i in state['items'] if i['id'] == child['id'])['parentId'] == parent['id'], 'Focused component sequencing stays inside its parent')
    state = call(f'/api/projects/{pid}/statuses', 'POST', {'expectedRevision': revision(), 'name': 'Review', 'color': '#778899'})
    review = next(s for s in state['statuses'] if s['projectId'] == pid and s['name'] == 'Review')
    state = call(f"/api/statuses/{review['id']}", 'PATCH', {'expectedRevision': revision(), 'name': 'Accepted', 'color': '#338855', 'isCompleted': True})
    check(next(s for s in state['statuses'] if s['id'] == review['id'])['isCompleted'], 'Workflow columns are editable and completion is configured')
    call(f'/api/statuses/{first}?expectedRevision={revision()}', 'DELETE', expected=400)
    check(any(s['id'] == first for s in call('/api/workspace')['statuses']), 'Nonempty column cannot be deleted')
    state = call(f"/api/statuses/{review['id']}?expectedRevision={revision()}", 'DELETE')
    race_revision = revision()
    def concurrent_move(status):
        try:
            return call(f"/api/items/{sibling['id']}/move", 'POST', {'expectedRevision': race_revision, 'parentId': None, 'statusId': status})
        except AssertionError as error:
            assert error.args[0][1] == 409, error
            return 'conflict'
    with concurrent.futures.ThreadPoolExecutor(2) as pool:
        results = list(pool.map(concurrent_move, [first, last]))
    check(sum(r == 'conflict' for r in results) == 1, 'Two simultaneous edits produce one commit and one conflict')
    state = call('/api/workspace')
    state = call(f"/api/items/{parent['id']}?expectedRevision={revision()}", 'DELETE')
    remaining = {i['id'] for i in state['items']}
    check(not remaining.intersection([parent['id'], child['id'], grandchild['id']]) and sibling['id'] in remaining, 'Subtree deletion removes descendants and preserves unrelated tasks')
    check(not any(d['projectId'] == pid for d in state['dependencies']), 'Subtree deletion removes all affected dependency links')
    call('/api/projects', 'POST', {'name': ' ', 'description': '', 'color': '#334455'}, 400)
    check(True, 'Blank project titles are rejected')
    call('/api/projects', 'POST', {'name': 'Null colour', 'description': None, 'color': None}, 400)
    check(True, 'Malformed colour yields validation instead of an internal error')
    call(f"/api/items/{sibling['id']}", 'PATCH', fields(sibling) | {'specJson': None}, 400)
    check(True, 'Null specifications are rejected without corrupting saved tasks')
    call(f'/api/projects/{pid}/items', 'POST', {'expectedRevision': revision(), 'title': 'Bad parent', 'parentId': str(uuid.uuid4()), 'statusId': first}, 404)
    check(True, 'Missing parents are rejected')
    print(f'\n{count} API / DuckDB checks passed.')
finally:
    for person_id in created_people:
        snapshot = call('/api/workspace')
        if any(p['id'] == person_id for p in snapshot['people']):
            call(f'/api/people/{person_id}', 'DELETE')
    for pid in created:
        snapshot = call('/api/workspace')
        project = next((p for p in snapshot['projects'] if p['id'] == pid), None)
        if project:
            call(f"/api/projects/{pid}?expectedRevision={project['revision']}", 'DELETE')
