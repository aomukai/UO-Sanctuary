#!/usr/bin/env python3
"""Record the current Druid cohort without claiming full executable parity."""
import argparse
from collections import Counter
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/druid-planned-acceptance'
sys.path.insert(0, str(ROOT / 'tests'))
from check_druid_planned_acceptance import configuration, ELEMENT_INPUTS


def expected_markers():
    config = configuration()
    expected = Counter()

    def add(command, count):
        expected[command] += count
        results = config['extra_wait_results'].get(command, ())
        for name in ((results,) if isinstance(results, str) else results):
            expected[name] += count

    add('iron-golem-birth-check', 3)
    for family in ('iron-golem-items', 'npc-resource-callbacks', *config['extra_families']):
        add(family + '-check', 1)
        add(family + '-reload-check', 2)
    for command in config['extra_checks']:
        add(command, 3)
    for command in config['extra_target_input_commands']:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    for command in config['extra_live_commands']:
        if command in config.get('checked_live_commands', ()):
            expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    for command in config['extra_pre_save_commands']:
        expected[command] += 3
    for name in ELEMENT_INPUTS:
        expected[name + '-input-check'] += 3
        expected[name + '-input-result'] += 3
    for name in ('shock-wave-live-check', 'shock-wave-live-result',
                 'arctic-held-input-save-stage-check', 'druid-fury-stage-check',
                 'druid-fury-stage-result'):
        expected[name] += 3
    for source in (0, 1, 9):
        expected[f'arctic-held-wire-{source}-result'] += 3
    assert sum(expected.values()) == 675
    return expected


def record(check=False):
    run = STAGE / 'native-run-7.log'
    assert 'pass across3boots/2reloads' in run.read_text(), 'Combined run has not succeeded'
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-17.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    reuse = e.load(STAGE / 'artifact-reuse.json')
    assert e.sha(ROOT / reuse['parentCheckpoint']) == reuse['parentCheckpointSha256']
    assert born['copiedData'] == reuse['copiedData']
    for name, digest in born['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == reuse['artifacts'][name], name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name]
    baseline = e.load(STAGE / 'source-snapshot-1.json')['inputs']['sourceInputs']
    changed = {name for name, digest in baseline.items() if born['sourceInputs'][name] != digest}
    assert changed == {'tests/Sanctuary.Integration/' + name + '.cs' for name in (
        'DruidHumanCastClockChecks', 'DruidRabiesChecks', 'DruidRabiesInfectionChecks',
        'DruidRabiesLifecycleChecks', 'ArcticStreamChecks', 'ArcticHeldInputChecks',
        'DruidFormWorldChecks', 'DruidFormCastingChecks', 'DruidFormInterruptionChecks',
        'DruidFormReactionChecks', 'DruidMaulChecks', 'DruidFeralChecks',
        'DruidFireClawsChecks', 'DruidHungerChecks', 'DruidShockWaveCastingChecks')}
    build = (STAGE / 'integration-build-15.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    historical_inputs = set()
    for name, count, same in (
        ('druid-clock-current-native-evidence.json', 33, False),
        ('druid-rabies-composition-native-evidence.json', 27, False),
        ('druid-arctic-composition-native-evidence.json', 39, False),
        ('druid-firestorm-order-native-evidence.json', 21, False),
        ('druid-form-retained-native-evidence.json', 165, False),
        ('druid-maul-expiry-form-native-evidence.json', 24, True),
    ):
        focused = e.load(ROOT / 'tools/reference' / name)
        assert focused['nativeMarkers'] == count
        for artifact, digest in focused['artifacts'].items():
            if same or artifact != 'Assemblies/Sanctuary.Integration.dll':
                assert digest == born['artifacts'][artifact], artifact
        for path, digest in focused['files'].items():
            if e.sha(ROOT / path) != digest:
                # Historical focused runs used earlier fixture inputs. Keep
                # their accepted receipts unchanged and authenticate each exact
                # saved input; the current run binds its own inputs above.
                assert path == 'tests/check_druid_planned_acceptance.py' or path in changed, path
                frozen_input = next((STAGE / folder / 'sources' / path
                    for folder in ('failed-run-4', 'failed-run-6')
                    if (STAGE / folder / 'sources' / path).is_file()
                    and e.sha(STAGE / folder / 'sources' / path) == digest), None)
                assert frozen_input is not None, path
                historical_inputs.add(frozen_input)
    config = configuration()
    assert config['extra_early_reload_families'] == ('firestorm',)
    assert tuple(len(config[key]) for key in ('extra_families', 'extra_checks',
        'extra_target_input_commands', 'extra_live_commands', 'extra_files',
        'extra_pre_save_commands')) == (57, 23, 43, 4, 146, 16)
    log = e.cohort(STAGE, expected_markers(), 675)
    connections = sum(expected_markers()[name] for name in config['extra_connected_checks'])
    connections += 3 * (len(config['extra_target_input_commands'])
        + len(config['extra_live_commands']) + len(ELEMENT_INPUTS) + 6)
    assert connections == 201 and log.count('Client: 127.0.0.1: Connected.') == connections
    assert 'denied by connection filter' not in log
    status = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert status['contentScope']['charms'] is False and status['contentScope']['runewords'] is False
    skills = [row for row in status['skills'] if row['characterClass'] == 'Druid']
    assert len(skills) == 30 and all(row['implementation'] == 'handler-present' for row in skills)
    files = [ROOT / 'tools/record_druid_planned_acceptance.py',
        ROOT / 'tests/check_druid_planned_acceptance.py', STAGE / 'birth-inputs.json',
        STAGE / 'birth-native-checks.log', run, STAGE / 'artifact-reuse.json',
        STAGE / 'source-snapshot-1.json', STAGE / 'source-snapshot-17.json',
        STAGE / 'integration-build-15.log']
    files += list(historical_inputs)
    for number in (1, 2, 3, 4, 5, 6):
        failed = STAGE / f'failed-run-{number}'
        files += [failed / name for name in ('birth-inputs.json', 'birth-native-checks.log',
            'sources/tests/check_druid_planned_acceptance.py', 'sources/tests/check_iron_golem_birth.py')]
    files += [ROOT / 'tools/reference' / name for name in (
        'item-content-scope-decision.json', 'summon-snapshot-decision.json',
        'druid-clock-current-native-evidence.json', 'druid-rabies-composition-native-evidence.json',
        'druid-arctic-composition-native-evidence.json', 'druid-firestorm-order-native-evidence.json',
        'druid-form-retained-native-evidence.json', 'druid-maul-expiry-form-native-evidence.json')]
    files += [ROOT / 'docs' / name for name in ('druid-planned-acceptance-checkpoint.md',
        'druid-coverage-matrix.md', 'arctic-approved-input-rule.md', 'skill-effects.md', 'skill-icons.md')]
    result = dict(component='Current planned-content Druid combined acceptance',
        referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=15,
        sourceSnapshot=17, nativeRun=7, nativeMarkers=675,
        markers=dict(sorted(expected_markers().items())), fixtureFamilies=57,
        sharedChecks=23, spellInputAttempts=129, legacyElementalSequences=27,
        heldArcticSequences=9, connectedFurySaveStages=3,
        connections=201, boots=3, saves=3, reloads=2,
        changedSources=sorted(changed), gameplayChanged=False, runtimeDataChanged=False,
        artifacts=born['artifacts'], contentScope=status['contentScope'], installed=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        goalComplete=False, classCertified=False,
        limits='Scoped class cohort, not full modern D2R parity. Current executable timing, RNG, mitigation/event ordering, geometry, recipients, owner/PvP and exceptional world cases retain documented limits. Current births and explicit historical summon profiles remain distinct; final effects/audio/icons, graphical-client acceptance and historical save-cleanup concerns remain separate.')
    e.write('druid-planned-acceptance-native-evidence.json', result, files, check)
    print('Druid planned cohort:675 exact results/3boots/2reloads; no class certification.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
