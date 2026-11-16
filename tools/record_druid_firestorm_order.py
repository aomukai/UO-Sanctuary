#!/usr/bin/env python3
"""Bind Firestorm's exact reload-before-new-casts order to unchanged binaries."""
import argparse
from collections import Counter

import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills/druid-planned-acceptance'
STAGE = ROOT / '.local/class-skills/druid-firestorm-clock-input-early'
NEGATIVE = ROOT / '.local/class-skills/druid-firestorm-clock-input-original'


def record(check=False):
    run = BASE / 'firestorm-clock-input-early-run-1.log'
    assert 'Firestorm clock/input composition:pass across3boots/2reloads' in run.read_text()
    assert 'retained wire: early' in run.read_text()
    assert 'retained wire: original' in (BASE / 'firestorm-clock-input-original-run-1.log').read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    assert born == e.load(NEGATIVE / 'birth-inputs.json'), 'Order controls changed tested inputs'
    snapshot(BASE / 'source-snapshot-14.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    frozen = e.load(BASE / 'failed-run-4/birth-inputs.json')
    assert born['artifacts'] == frozen['artifacts'] and born['copiedData'] == frozen['copiedData']
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    expected = Counter({'iron-golem-birth-check': 3, 'druid-human-cast-clock-check': 3,
        'firestorm-input-check': 3, 'firestorm-input-result': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'firestorm'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    log = e.cohort(STAGE, expected, 21)
    assert log.count('Client: 127.0.0.1: Connected.') == 3
    negative = (NEGATIVE / 'birth-native-checks.log').read_text()
    bad = Counter({'iron-golem-birth-check': 2, 'druid-human-cast-clock-check': 2,
        'firestorm-input-check': 1, 'firestorm-input-result': 1,
        'firestorm-check': 1, 'iron-golem-items-check': 1,
        'iron-golem-items-reload-check': 1, 'npc-resource-callbacks-check': 1,
        'npc-resource-callbacks-reload-check': 1})
    assert markers(NEGATIVE / 'birth-native-checks.log') == bad
    assert negative.count(' ERR]') == 1 and 'firestorm-reload-check: failed' in negative
    assert 'Exact world emitter/patch/seed/life reload' in negative
    files = [ROOT / 'tools/record_druid_firestorm_order.py',
        ROOT / 'tests/check_druid_planned_acceptance.py',
        BASE / 'firestorm-clock-input-order-driver.py', BASE / 'source-snapshot-14.json',
        BASE / 'failed-run-4/birth-inputs.json',
        BASE / 'firestorm-clock-input-early-run-1.log', BASE / 'firestorm-clock-input-original-run-1.log']
    files += [folder / name for folder in (STAGE, NEGATIVE)
              for name in ('birth-inputs.json', 'birth-native-checks.log')]
    result = dict(component='Firestorm saved-state check precedes new clock casts',
        referenceVersion='3.3.93847', contentBuildReused=1, integrationBuildReused=12,
        sourceSnapshot=14, nativeMarkers=21, markers=dict(expected), spellInputs=3,
        connections=3, boots=3, saves=3, reloads=2,
        identicalPositiveNegativeInputs=True, expectedNegativeMarkers=11,
        artifacts=born['artifacts'], gameplayChanged=False, runtimeDataChanged=False,
        csharpChanged=False, installed=False, classCertified=False,
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='The original order deterministically fails after new legitimate clock casts; checking exact saved state first and retaining normal input checkpointing passes two reloads. No weakened world, packet, RNG, life, binding, mana or no-replay assertion. Scope is fixture ordering; combined Druid acceptance remains pending.')
    e.write('druid-firestorm-order-native-evidence.json', result, files, check)
    print('Firestorm order:21 exact positives/two reloads; identical-input negative retained.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
