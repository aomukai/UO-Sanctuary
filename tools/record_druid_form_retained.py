#!/usr/bin/env python3
"""Bind retained form inspection to independent natural and offline expiry."""
import argparse
from collections import Counter
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills/druid-planned-acceptance'
STAGE = ROOT / '.local/class-skills/druid-form-retained-current-3'
sys.path.insert(0, str(ROOT / 'tests'))
from check_druid_planned_acceptance import configuration


def expected_markers():
    config = configuration()
    families = tuple(f for f in config['extra_families']
        if (f.startswith('druid-') and f != 'druid-snapshot')
        or f.startswith('shock-wave') or f == 'shape-restriction')
    checks = tuple(n for n in config['extra_checks']
        if n.startswith('druid-') and n != 'druid-human-cast-clock-check')
    saves = tuple(n for n in config['extra_pre_save_commands']
        if n != 'arctic-stream-expiry-stage-check')
    assert (len(families), len(checks), len(saves)) == (16, 12, 15)
    expected = Counter({'iron-golem-birth-check': 3})

    def add(name, count):
        expected[name] += count
        results = config['extra_wait_results'].get(name, ())
        for result in ((results,) if isinstance(results, str) else results):
            expected[result] += count

    for family in ('iron-golem-items', 'npc-resource-callbacks', *families):
        add(family + '-check', 1)
        add(family + '-reload-check', 2)
    for name in (*checks, *saves):
        add(name, 3)
    for name in ('shock-wave-live-check', 'shock-wave-live-result',
                 'druid-fury-stage-check', 'druid-fury-stage-result'):
        expected[name] += 3
    assert sum(expected.values()) == 165
    return expected


def record(check=False):
    run = BASE / 'form-retained-focused-run-3.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(BASE / 'source-snapshot-16.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    reuse = e.load(BASE / 'artifact-reuse.json')
    assert born['copiedData'] == reuse['copiedData']
    assert e.sha(ROOT / reuse['parentCheckpoint']) == reuse['parentCheckpointSha256']
    for name, digest in born['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == reuse['artifacts'][name], name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    before = e.load(BASE / 'failed-run-5/birth-inputs.json')['sourceInputs']
    changed = {name for name, digest in before.items()
               if name.endswith('.cs') and digest != born['sourceInputs'][name]}
    assert changed == {'tests/Sanctuary.Integration/' + name + 'Checks.cs' for name in (
        'DruidFormWorld', 'DruidFormCasting', 'DruidFormInterruption',
        'DruidFormReaction', 'DruidMaul', 'DruidFeral', 'DruidFireClaws',
        'DruidHunger', 'DruidRabies', 'DruidShockWaveCasting')}
    build = (BASE / 'integration-build-14.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    log = e.cohort(STAGE, expected_markers(), 165)
    assert log.count('Client: 127.0.0.1: Connected.') == 6
    assert 'denied by connection filter' not in log
    files = [ROOT / 'tools/record_druid_form_retained.py',
        ROOT / 'tests/check_druid_planned_acceptance.py',
        ROOT / 'tests/check_iron_golem_birth.py',
        BASE / 'form-retained-driver-3.py', BASE / 'form-retained-focused-run-3.log',
        BASE / 'source-snapshot-16.json', BASE / 'integration-build-14.log',
        BASE / 'artifact-reuse.json', BASE / 'failed-run-5/birth-inputs.json',
        BASE / 'failed-run-5/birth-native-checks.log',
        BASE / 'failed-form-retained-focus-2/birth-inputs.json',
        BASE / 'failed-form-retained-focus-2/birth-native-checks.log',
        BASE / 'failed-form-retained-focus-2/form-retained-driver-2.py',
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log']
    files += [ROOT / name for name in changed]
    result = dict(component='Retained Druid forms with unchanged independent expiry controls',
        referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=14,
        sourceSnapshot=16, nativeRun=3, nativeMarkers=165,
        markers=dict(sorted(expected_markers().items())), fixtureFamilies=16,
        sharedChecks=12, saveControls=15, connections=6,
        boots=3, saves=3, reloads=2, changedSources=sorted(changed),
        gameplayChanged=False, runtimeDataChanged=False, installed=False,
        artifacts=born['artifacts'], classCertified=False,
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Inspection-only helper shifts both UTC endpoints equally after actual casts/contacts; captured payload, duration, body and fractional resources remain exact. Natural/offline form and charge expiry plus paid Fury no-replay retain their real deadlines. Existing connected Shock Wave control is included. Combined Druid acceptance remains pending.')
    e.write('druid-form-retained-native-evidence.json', result, files, check)
    print('Retained forms:165 exact results/3saves/2reloads; natural expiry unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
