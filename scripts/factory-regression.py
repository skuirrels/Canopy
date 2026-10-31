#!/usr/bin/env python3
"""Exercise factory configuration through real HTTP and native DuckDB, using only temporary data."""
import copy
import concurrent.futures
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / 'server/Planner.Api'
checks = 0


def check(value, message):
    global checks
    assert value, message
    checks += 1
    print(f'PASS {checks:02}: {message}', flush=True)


with tempfile.TemporaryDirectory(prefix='canopy-factory-') as directory:
    scratch = Path(directory)
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        base = f'http://127.0.0.1:{sock.getsockname()[1]}'
    env = os.environ | {'Database__Path': str(scratch / 'workspace.duckdb'), 'Agents__WorkspaceRoot': str(scratch / 'worktrees')}
    log = open(scratch / 'server.log', 'w+')
    process = None

    def call(path, method='GET', body=None, expected=200, headers=None):
        request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
                                        headers={'Content-Type': 'application/json'} | (headers or {}), method=method)
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                code, raw = response.status, response.read()
        except urllib.error.HTTPError as error:
            code, raw = error.code, error.read()
        value = json.loads(raw) if raw else None
        assert code == expected, (path, code, expected, value)
        return value

    def start():
        global process
        process = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base], cwd=API, env=env, stdout=log, stderr=log)
        for _ in range(150):
            if process.poll() is not None:
                log.seek(0)
                raise AssertionError(log.read())
            try:
                call('/api/health')
                return
            except OSError:
                time.sleep(.1)
        raise AssertionError('Backend did not start')

    def stop():
        process.terminate()
        process.wait(timeout=20)

    def state():
        return call('/api/workspace')

    def revision(project=None):
        return next(p['revision'] for p in state()['projects'] if p['id'] == (project or pid))

    def create(name, **configuration):
        return call(f'/api/factory/projects/{pid}/pipelines', 'POST', dict(expectedRevision=revision(), name=name, description='Test only', **configuration), 201)

    def change(detail, action, expected=200, **extra):
        return call(f"/api/factory/pipelines/{detail['pipeline']['id']}/{action}", 'PUT' if action == 'draft' else 'POST',
                    dict(expectedRevision=revision(), expectedDraftRevision=detail['pipeline']['draftRevision'], **extra), expected)

    def validate(definition):
        before = revision()
        result = call('/api/factory/validate', 'POST', definition)
        assert revision() == before, 'Validation must be read-only'
        return result

    try:
        start()
        templates = call('/api/factory/templates')
        configured = json.loads((API / 'factory-templates.json').read_text())['FactoryTemplates']
        check([t['id'] for t in templates] == [t['Id'] if 'Id' in t else t['id'] for t in configured], 'template catalogue comes from configuration')
        workspace = call('/api/projects', 'POST', dict(name='Factory regression', description='Temporary', color='#376881'))
        pid = workspace['projects'][-1]['id']
        workspace = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Factory task'))
        tid = next(i['id'] for i in workspace['items'] if i['projectId'] == pid)
        task_endpoint = f'/api/factory/tasks/{tid}/configuration'
        before_item = state()['items'][0]
        check(call(task_endpoint)['configuration'] is None, 'ordinary tasks have no factory attachment')
        check(call(f'/api/factory/projects/{pid}/pipelines')['pipelines'] == [], 'startup inserts no pipelines or example projects')
        specification = next(t for t in templates if t['id'] == 'specification')
        valid = validate(specification['definition'])
        check(valid['valid'] and valid['executionReadiness'] == 'notEvaluated', 'structural validity never claims execution readiness')
        detail = create('Writing pipeline', templateId=specification['id'])
        pipeline_id = detail['pipeline']['id']
        check(detail['pipeline']['source'] == specification['source'] and detail['versions'] == [], 'template creates an editable draft with configured provenance')
        published = change(detail, 'publish')
        version = published['versions'][0]
        check(version['definition'] == detail['pipeline']['definition'] and version['number'] == 1, 'publish snapshots the complete definition')
        change(published, 'publish', 409)
        check(True, 'publishing the unchanged draft twice is rejected')
        attached = call(task_endpoint, 'PUT', dict(expectedRevision=revision(), pipelineVersionId=version['id'], overrides=dict(steps=[])))
        check(attached['version'] == version and not attached['updateAvailable'], 'task attachment pins the published version')
        stale_project_revision = revision()
        updated = change(published, 'draft', name='Renamed draft', description='Changed', definition=version['definition'])
        check(call('/api/factory/versions/' + version['id']) == version, 'editing a draft never mutates the published version')
        call('/api/factory/pipelines/' + pipeline_id + '/draft', 'PUT', dict(expectedRevision=stale_project_revision, expectedDraftRevision=updated['pipeline']['draftRevision'], name='Stale project', description='', definition=version['definition']), 409)
        change(published, 'draft', 409, name='Stale draft', description='', definition=version['definition'])
        check(True, 'project and draft revision conflicts both reject stale writes')
        second = change(updated, 'publish')
        task = call(task_endpoint)
        check(task['version'] == version and task['updateAvailable'], 'publishing version two advertises an update without adopting it')
        override = dict(stageId=version['definition']['stages'][0]['id'], stepId=version['definition']['stages'][0]['steps'][0]['id'], instructions='Task-specific writing instructions', provider='gemini', model='test-model')
        task = call(task_endpoint, 'PUT', dict(expectedRevision=revision(), pipelineVersionId=version['id'], overrides=dict(steps=[override])))
        check(task['resolvedDefinition']['stages'][0]['steps'][0]['instructions'] == override['instructions'] and task['resolvedDefinition']['stages'][0]['steps'][0]['provider'] == 'gemini' and task['version'] == version, 'explicit task overrides resolve without changing the version')
        for steps in [[dict(override, stepId='missing')], [override, override], [dict(override, provider='unknown')]]:
            call(task_endpoint, 'PUT', dict(expectedRevision=revision(), pipelineVersionId=version['id'], overrides=dict(steps=steps)), 400)
        check(call(task_endpoint) == task, 'invalid override references, duplicates and providers roll back atomically')
        other = call('/api/projects', 'POST', dict(name='Other factory project', description='', color='#376881'))['projects'][-1]['id']
        foreign = call(f'/api/factory/projects/{other}/pipelines', 'POST', dict(expectedRevision=revision(other), name='Foreign', description='', templateId=specification['id']), 201)
        foreign = call(f"/api/factory/pipelines/{foreign['pipeline']['id']}/publish", 'POST', dict(expectedRevision=revision(other), expectedDraftRevision=foreign['pipeline']['draftRevision']))
        call(task_endpoint, 'PUT', dict(expectedRevision=revision(), pipelineVersionId=foreign['versions'][0]['id'], overrides=dict(steps=[])), 400)
        check(call(task_endpoint) == task, 'cross-project published versions cannot attach to a task')
        standard = next(t for t in templates if t['id'] == 'standard-implementation')
        incomplete = create('Implementation', templateId=standard['id'])
        result = validate(incomplete['pipeline']['definition'])
        check(any(d['code'] == 'command.required' and d['stageId'] == 'verify' and d['stepId'] == 'checks' for d in result['diagnostics']), 'missing verification command identifies the exact stage and field')
        change(incomplete, 'publish', 400)
        check(True, 'incomplete drafts cannot be published')
        definition = copy.deepcopy(incomplete['pipeline']['definition'])
        command = dict(executable='python3', arguments=['-m', 'unittest'], workingDirectory='.', timeoutSeconds=60, environmentReferences={'TEST_TOKEN': 'FACTORY_TEST_TOKEN'})
        definition['stages'][2]['steps'][0]['command'] = command
        check(validate(definition)['valid'], 'explicit command inputs produce a structurally valid implementation pipeline')
        custom = create('Custom', definition=definition, source='Regression public API input')
        custom = change(custom, 'publish')
        check(custom['versions'][0]['definition'] == definition, 'custom pipeline uses the same public persistence and publishing path')
        race_pipeline = create('Concurrent publication', templateId=specification['id'])
        publish_path = '/api/factory/pipelines/' + race_pipeline['pipeline']['id'] + '/publish'
        publish_input = dict(expectedRevision=revision(), expectedDraftRevision=race_pipeline['pipeline']['draftRevision'])

        def publish_concurrently(_):
            request = urllib.request.Request(base + publish_path, data=json.dumps(publish_input).encode(), headers={'Content-Type': 'application/json'}, method='POST')
            try:
                with urllib.request.urlopen(request, timeout=20) as response:
                    return response.status
            except urllib.error.HTTPError as error:
                return error.code

        with concurrent.futures.ThreadPoolExecutor(2) as pool:
            codes = list(pool.map(publish_concurrently, range(2)))
        check(sorted(codes) == [200, 409] and len(call('/api/factory/pipelines/' + race_pipeline['pipeline']['id'])['versions']) == 1, 'simultaneous publications produce one version and one revision conflict')
        for label, mutate, code in [
            ('cycle', lambda d: d['stages'][0]['dependsOn'].append('handoff'), 'graph.cycle'),
            ('unknown dependency', lambda d: d['stages'][0]['dependsOn'].append('missing'), 'dependency.invalid'),
            ('duplicate stage', lambda d: d['stages'].append(copy.deepcopy(d['stages'][0])), 'stage.duplicate'),
            ('self dependency', lambda d: d['stages'][0]['dependsOn'].append('prepare'), 'dependency.invalid'),
            ('missing artifact', lambda d: d['stages'][2]['steps'][0].update(consumes=['unknown']), 'artifact.unavailable'),
            ('duplicate artifact', lambda d: d['stages'][2]['steps'][0].update(produces=['changes']), 'artifact.duplicate'),
            ('future artifact', lambda d: d['stages'][1]['steps'][0].update(consumes=['verification']), 'artifact.unavailable'),
            ('writeable review', lambda d: d['stages'][3]['steps'][0].update(permission='workspace-write'), 'review.permission'),
            ('traversal directory', lambda d: d['stages'][2]['steps'][0]['command'].update(workingDirectory='../outside'), 'command.directory'),
            ('absolute directory', lambda d: d['stages'][2]['steps'][0]['command'].update(workingDirectory='/tmp'), 'command.directory'),
            ('inline environment secret', lambda d: d['stages'][2]['steps'][0]['command'].update(environmentReferences={'TOKEN': 'secret-value!'}), 'command.environment'),
            ('invalid timeout', lambda d: d['stages'][2]['steps'][0]['command'].update(timeoutSeconds=0), 'command.timeout'),
            ('null stage', lambda d: d['stages'].append(None), 'stage.required'),
            ('null step', lambda d: d['stages'][0]['steps'].append(None), 'step.required'),
            ('null dependencies', lambda d: d['stages'][0].update(dependsOn=None), 'dependencies.required'),
            ('wrong step fields', lambda d: d['stages'][0]['steps'][0].update(provider='codex'), 'step.agentFields'),
            ('unsupported schema', lambda d: d.update(schemaVersion=99), 'definition.schema'),
            ('control character ID', lambda d: d['stages'][0].update(id='prepare\n'), 'stage.id'),
        ]:
            invalid = copy.deepcopy(definition)
            mutate(invalid)
            check(any(d['code'] == code for d in validate(invalid)['diagnostics']), label + ' returns a structured diagnostic')
            create_body = dict(expectedRevision=revision(), name='Invalid', description='', definition=invalid, source='Regression input')
            call(f'/api/factory/projects/{pid}/pipelines', 'POST', create_body, 400)
        incomplete_bad_graph = copy.deepcopy(standard['definition'])
        incomplete_bad_graph['stages'][1]['steps'][0]['consumes'] = ['missing']
        check(any(d['code'] == 'artifact.unavailable' for d in validate(incomplete_bad_graph)['diagnostics']), 'incomplete command does not hide invalid artifact wiring')
        call(task_endpoint, 'PUT', dict(expectedRevision=revision(), pipelineVersionId=version['id'], overrides=dict(steps=[dict(override, permission='workspace-write')])), 400)
        unknown = copy.deepcopy(definition)
        unknown['autoMerge'] = True
        call('/api/factory/validate', 'POST', unknown, 400)
        check(True, 'unknown configuration and privilege override fields are rejected rather than silently discarded')
        oversized = copy.deepcopy(definition)
        oversized['stages'][0]['steps'][0]['instructions'] = 'x' * 2_000_001
        call('/api/factory/validate', 'POST', oversized, 400)
        call(task_endpoint, 'PUT', dict(expectedRevision=revision(), pipelineVersionId=version['id'], overrides=dict(steps=[dict(override, instructions='x' * 2_000_001)])), 400)
        check(True, 'oversized definitions and resolved task overrides are rejected')
        for payload in [dict(templateId='missing'), dict(templateId=specification['id'], definition=definition), {}, dict(definition=definition)]:
            call(f'/api/factory/projects/{pid}/pipelines', 'POST', dict(expectedRevision=revision(), name='Invalid source', description='', **payload), 404 if payload == dict(templateId='missing') else 400)
        check(True, 'creation rejects ambiguous inputs, unknown templates and missing custom provenance')
        snapshot = call('/api/factory/pipelines/' + pipeline_id)
        task_snapshot = call(task_endpoint)
        stop()
        start()
        check(call('/api/factory/pipelines/' + pipeline_id) == snapshot and call(task_endpoint) == task_snapshot, 'drafts, all published versions and task overrides survive backend restart')
        archived = change(snapshot, 'archive')
        call(task_endpoint, 'PUT', dict(expectedRevision=revision(), pipelineVersionId=version['id'], overrides=dict(steps=[])), 409)
        check(call(task_endpoint)['archived'] and call(task_endpoint)['version'] == version, 'archiving prevents new attachments while retaining existing configuration and history')
        change(archived, 'draft', 409, name='Reopen', description='', definition=definition)
        check(True, 'archived pipelines reject draft changes')
        for headers in [{'Origin': 'https://example.invalid'}, {'Sec-Fetch-Site': 'cross-site'}]:
            call('/api/factory/templates', headers=headers, expected=403)
        check(True, 'factory API rejects cross-origin and cross-site access')
        for path in ['/api/factory/pipelines/', '/api/factory/versions/', '/api/factory/tasks/']:
            call(path + str(uuid.uuid4()) + ('/configuration' if path.endswith('tasks/') else ''), expected=404)
        check(True, 'unknown resources return 404')
        check(state()['items'][0] == before_item and call(f'/api/agents/tasks/{tid}/runs') == [] and not (scratch / 'worktrees').exists(), 'configuration never changes task hierarchy, status, dates or layout and never dispatches an agent')
        call(task_endpoint + f'?expectedRevision={revision()}', 'DELETE')
        check(call(task_endpoint)['configuration'] is None, 'detaching retains the task and pipeline versions')
        call(task_endpoint, 'PUT', dict(expectedRevision=revision(), pipelineVersionId=custom['versions'][0]['id'], overrides=dict(steps=[])))
        call(f'/api/items/{tid}?expectedRevision={revision()}', 'DELETE')
        check(state()['items'] == [], 'task deletion cleans its factory attachment')
        call(f'/api/projects/{pid}?expectedRevision={revision()}', 'DELETE')
        call('/api/factory/pipelines/' + pipeline_id, expected=404)
        call('/api/factory/versions/' + version['id'], expected=404)
        call(f'/api/projects/{other}?expectedRevision={revision(other)}', 'DELETE')
        stop()
        start()
        check(state()['projects'] == [] and state()['items'] == [], 'project cleanup persists across restart without orphan factory records')
        print(f'{checks} checks passed')
    finally:
        if process and process.poll() is None:
            stop()
        log.close()
