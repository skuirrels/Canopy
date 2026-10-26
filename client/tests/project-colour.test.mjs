import test from 'node:test';
import assert from 'node:assert/strict';
import { projectAccentTokens, contrastRatio, contrastTextColour, REACT_FLOW_PINK } from '../src/lib/project-colour.ts';

test('project accents retain the chosen source and remain readable on both themes', () => {
 for (const surface of ['#19191d', '#ffffff', '#fff']) {
  for (const colour of [REACT_FLOW_PINK, '#477fa1', '#2f7258', '#000000', '#ffffff', '#ffff00']) {
   const tokens = projectAccentTokens(colour, surface);
   assert.equal(tokens['--accent-source'], colour);
   assert.ok(contrastRatio(tokens['--accent'], surface) >= 4.5, `${colour} accent on ${surface}`);
   assert.ok(contrastRatio(tokens['--accent-fill'], '#ffffff') >= 4.5, `${colour} button label`);
   assert.ok(contrastRatio(tokens['--accent-soft-text'], tokens['--accent-soft']) >= 4.5, `${colour} selected label`);
   assert.ok(contrastRatio(contrastTextColour(colour), colour) >= 4.5, `${colour} initials`);
  }
 }
});
test('changing the project colour regenerates highlights rather than retaining pink variants', () => {
 const pink = projectAccentTokens(REACT_FLOW_PINK, '#19191d');
 const green = projectAccentTokens('#2f7258', '#19191d');
 for (const key of ['--accent', '--accent-fill', '--accent-hover', '--accent-border', '--accent-soft', '--accent-soft-strong', '--accent-soft-text']) assert.notEqual(green[key], pink[key], key);
});
