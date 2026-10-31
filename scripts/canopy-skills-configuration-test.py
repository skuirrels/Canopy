#!/usr/bin/env python3
"""Check registration boundaries using the actual project skill files."""
import importlib.util
import json
import os
from pathlib import Path
import stat
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('registration', Path(__file__).with_name('configure-canopy-skills.py'))
registration = importlib.util.module_from_spec(spec)
spec.loader.exec_module(registration)


class RegistrationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='canopy-skill-config-')
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name).resolve()
        self.config = self.root / 'appsettings.Local.json'
        self.original = {'Delivery': {'TokenEnvironmentVariable': 'CALLER_TOKEN_NAME'}, 'Factory': {'SkillDiscovery': {'Roots': [{'Source': 'Caller library', 'Path': str(self.root), 'MaximumDepth': 1}]}, 'Skills': [{'Id': 'caller-skill', 'Source': 'Caller library', 'Path': str(self.root / 'caller/SKILL.md')}]}}
        self.config.write_text(json.dumps(self.original))
        self.config.chmod(0o600)

    def test_preview_does_not_write(self):
        before = self.config.read_bytes()
        plan = registration.configure(self.config)
        self.assertTrue(plan['changed'])
        self.assertFalse(plan['applied'])
        self.assertEqual(before, self.config.read_bytes())

    def test_apply_preserves_settings_and_limits_edit_scope(self):
        plan = registration.configure(self.config, True)
        saved = json.loads(self.config.read_text())
        self.assertTrue(plan['applied'])
        self.assertEqual(self.original['Delivery'], saved['Delivery'])
        self.assertEqual(self.original['Factory']['SkillDiscovery'], saved['Factory']['SkillDiscovery'])
        self.assertEqual(self.original['Factory']['Skills'][0], saved['Factory']['Skills'][0])
        self.assertEqual(set(registration.NAMES), {x['Id'] for x in plan['skills']})
        allowed = {Path(x) for x in plan['editableRoots']}
        self.assertEqual({'canopy-qa', 'canopy-review'}, {x.name for x in allowed})
        self.assertNotIn(Path(plan['skills'][0]['Path']).parent, allowed)
        self.assertFalse(any(k.lower() in ('autorun', 'autoapply') for k in saved['Factory']['Calibration']))
        for entry in plan['skills']:
            self.assertTrue(Path(entry['Path']).is_file())
        if os.name != 'nt':
            self.assertEqual(0o600, stat.S_IMODE(self.config.stat().st_mode))

    def test_exact_retry_preserves_bytes_and_timestamp(self):
        registration.configure(self.config, True)
        before = (self.config.read_bytes(), self.config.stat().st_mtime_ns)
        result = registration.configure(self.config, True)
        self.assertFalse(result['changed'])
        self.assertFalse(result['applied'])
        self.assertEqual(before, (self.config.read_bytes(), self.config.stat().st_mtime_ns))

    def test_different_existing_mapping_is_not_overwritten(self):
        self.original['Factory']['Skills'].append({'Id': 'canopy-qa', 'Source': registration.SOURCE, 'Path': str(self.root / 'different/SKILL.md')})
        self.config.write_text(json.dumps(self.original))
        before = self.config.read_bytes()
        with self.assertRaises(ValueError):
            registration.configure(self.config, True)
        self.assertEqual(before, self.config.read_bytes())

    def test_broader_existing_authority_requires_explicit_review(self):
        self.original['Factory']['Calibration'] = {'EditableRoots': [str(self.root)]}
        self.config.write_text(json.dumps(self.original))
        before = self.config.read_bytes()
        with self.assertRaises(ValueError):
            registration.configure(self.config, True)
        self.assertEqual(before, self.config.read_bytes())

    def test_case_insensitive_settings_keep_one_owner(self):
        self.config.write_text(json.dumps({'factory': {'skills': [], 'calibration': {'editableroots': []}}}))
        registration.configure(self.config, True)
        saved = json.loads(self.config.read_text())
        self.assertEqual(['factory'], list(saved))
        self.assertEqual(3, len(saved['factory']['skills']))
        self.assertEqual(2, len(saved['factory']['calibration']['editableroots']))

    def test_duplicate_keys_and_malformed_configuration_fail_without_write(self):
        for content in ['{"Factory":{},"factory":{}}', '{', '{"Factory":{"Skills":null}}']:
            with self.subTest(content=content):
                self.config.write_text(content)
                with self.assertRaises(ValueError):
                    registration.configure(self.config, True)
                self.assertEqual(content, self.config.read_text())

    def test_symbolic_link_is_not_a_configuration_destination(self):
        destination = self.root / 'destination.json'
        self.config.rename(destination)
        self.config.symlink_to(destination)
        before = destination.read_bytes()
        with self.assertRaises(ValueError):
            registration.configure(self.config, True)
        self.assertEqual(before, destination.read_bytes())


if __name__ == '__main__':
    unittest.main(verbosity=2)
