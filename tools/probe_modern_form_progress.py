#!/usr/bin/env python3
"""Expose the pinned calculator's actual frame advance in addition to its cycles."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    prior_path = ROOT / 'tools/reference/modern-form-rate-source-evidence.json'
    prior = json.loads(prior_path.read_text())
    harness = BASE / 'modern-form-rate-source-probe.mjs'
    assert sha(harness) == prior['harnessSha256']
    folder = ROOT / prior['source']['cache']
    for name, digest in prior['source']['files'].items():
        assert sha(folder / name) == digest, name
    text = harness.read_text()
    old = 'const calculator=new vm.SourceTextModule(original.replace(marker,expose),{context});'
    replacement = r'''
const anchor='let firstHitLength = 256 * (framesPerDirection1 - startingFrame) / speedIncrease;';
if(original.split(anchor).length!==2)throw Error('Frame-advance tap changed');
// Observe the first loop iteration without changing any numerical operation.
const tapped=original.replace(anchor,'if(acceleration===startingAcceleration)globalThis.referenceProgress=speedIncrease;'+anchor);
const calculator=new vm.SourceTextModule(tapped.replace(marker,expose),{context});
'''
    assert text.count(old) == 1
    text = text.replace(old, replacement, 1)
    old = 'limitEIAS(values[0]),table[0][1]];'
    assert text.count(old) == 1
    text = text.replace(old, 'limitEIAS(values[0]),table[0][1],globalThis.referenceProgress];', 1)
    generated = BASE / 'modern-form-progress-source-probe.mjs';generated.write_text(text)
    process = subprocess.run(['node', '--no-warnings', '--experimental-vm-modules', str(generated), str(folder)], capture_output=True)
    log = BASE / 'modern-form-progress-source-probe.log';log.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()
    cases = json.loads(process.stdout)
    baseline_path = ROOT / prior['fixture']['path']
    assert sha(baseline_path) == prior['fixture']['sha256']
    baseline = json.loads(baseline_path.read_text())
    assert len(cases) == len(baseline) == 14400
    for row, old in zip(cases, baseline, strict=True):
        assert row[:7] == old[:7] and row[7][:-1] == old[7]
        assert isinstance(row[7][8], int) and row[7][8] > 0
        shape_frames = row[7][2];progress = row[7][8]
        assert row[7][7] == (shape_frames * 256 + progress - 1) // progress - 1
    fixture = BASE / 'modern-form-progress-source-probe.json';fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', supplementaryResearch=True, nativeAccepted=False, currentExecutableParity=False, contexts=14400,
        predecessor=dict(path=str(prior_path.relative_to(ROOT)), sha256=sha(prior_path), sourceCommit=prior['source']['commit']),
        sourceFiles=prior['source']['files'], fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)), harnessSha256=sha(generated), stderrSha256=sha(log),
        established=[
            'The existing pinned3.3author-calculator harness now observes its actual first-iteration speedIncrease value through a non-mutating tap. All14400original inputs,EIAS fields and cycle outputs remain byte-value identical. Numerical routines/loop operations remain unchanged.',
            'Frame advance is directly observed,not inferred from an output cycle that could hide a one-unit difference. This prepares native Progress256comparisons for the same bounded single-weapon Druid standard-attack domain.',
        ], remaining=prior['remaining'])
    path = ROOT / 'tools/reference/modern-form-progress-source-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Modern form progression:14400actual author-loop advance values,unchanged prior EIAS/cycles; no native timing/event consumer accepted.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
