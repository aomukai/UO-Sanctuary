#!/usr/bin/env python3
"""Seal the test-only Blade Fury input proposal; no gameplay approval implied."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'blade-fury-held-candidate'
WIDE = BASE / 'blade-fury-held-candidate-retained'
PARENT = 'f54bda64e5d6acc4b96e433a84ff0b13ae7e772d'


def record(check=False):
    prebuild = STAGE / 'source-snapshot-2.json'
    current = snapshot(prebuild)['inputs']
    old = e.load(BASE / 'arctic-held-input/source-snapshot-3.json')['inputs']
    assert current['sourceInputs'].keys() - old['sourceInputs'].keys() == {
        'tests/Sanctuary.Integration/BladeFuryHeldCandidateChecks.cs'}
    assert all(current['sourceInputs'][p] == h for p, h in old['sourceInputs'].items())
    assert current['projectInputs'] == old['projectInputs'] and current['copiedData'] == old['copiedData']
    inventories = [e.load(s / 'birth-inputs.json') for s in (STAGE, WIDE)]
    assert inventories[0]['artifacts'] == inventories[1]['artifacts']
    assert inventories[0]['copiedData'] == inventories[1]['copiedData'] == current['copiedData']
    for stage, inputs in zip((STAGE, WIDE), inventories):
        snapshot(prebuild, stage / 'birth-inputs.json')
        for p, h in inputs['sourceInputs'].items():
            assert e.sha(ROOT / p) == h, p
        for kind in ('Content', 'Integration'):
            assert e.sha(stage / kind.lower() / f'Sanctuary.{kind}.dll') == inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    parent = e.load(ROOT / 'tools/reference/arctic-held-input-native-evidence.json')
    old_artifacts = e.load(BASE / 'arctic-held-input/birth-inputs.json')['artifacts']
    assert all(inventories[0]['artifacts'][p] == h for p, h in old_artifacts.items()
               if p != 'Assemblies/Sanctuary.Integration.dll')
    assert parent['handlers'] == 204
    build = (STAGE / 'integration-build-2.log').read_text()
    assert all(s in build for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    focus = Counter({'iron-golem-birth-check': 3, 'iron-golem-items-check': 1,
                     'npc-resource-callbacks-check': 1, 'blade-fury-held-candidate-check': 1,
                     'iron-golem-items-reload-check': 2, 'npc-resource-callbacks-reload-check': 2,
                     'blade-fury-held-candidate-reload-check': 2})
    earlier = ROOT / 'tools/reference/blade-fury-reentry-native-evidence.json'
    earlier_receipt = e.load(earlier)
    earlier_log = BASE / 'blade-fury-reentry/birth-native-checks.log'
    assert e.sha(earlier_log) == earlier_receipt['evidence'][str(earlier_log.relative_to(ROOT))]
    retained = markers(earlier_log)
    assert sum(retained.values()) == 228
    retained.update({'blade-fury-held-candidate-check': 1, 'blade-fury-held-candidate-reload-check': 2})
    logs = [e.cohort(STAGE, focus, 12), e.cohort(WIDE, retained, 231)]
    for stage, run, log in zip((STAGE, WIDE), (2, 1), logs):
        assert 'pass across3boots/2reloads' in (stage / f'run-{run}.log').read_text()
        assert log.count('first blade frame12,then5frames') == 1
        saved = e.load(stage / 'blade-fury-held-candidate-fixtures.json')
        assert saved['Generation'] == 2 and saved['Mana256'] == 72 * 256 and len(saved['Flights']) == 8
        assert all(f['Owner'] == saved['Owner'] and f['SkillRank'] == 1 and f['Ability'] == 207
                   and f['MechanicalProfile'] == 'source-player-missile-plus-current-fury-v1' for f in saved['Flights'])
    failed = STAGE / 'failed-cadence-i1'
    failed_inputs = e.load(failed / 'birth-inputs.json')
    assert e.sha(failed / 'BladeFuryHeldCandidateChecks.cs') == failed_inputs['sourceInputs']['tests/Sanctuary.Integration/BladeFuryHeldCandidateChecks.cs']
    assert e.sha(failed / 'integration/Sanctuary.Integration.dll') == failed_inputs['artifacts']['Assemblies/Sanctuary.Integration.dll']
    assert failed_inputs['artifacts']['Assemblies/Sanctuary.Content.dll'] == inventories[0]['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert 'Candidate phase/payment mismatch: 12,17,22,27,32,37,42,47 mana=18432' in (failed / 'birth-native-checks.log').read_text()
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python
    status = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation'] == 'handler-present' for r in status['skills']) == 204
    assert not status['completedClasses']
    protected = preservation()
    client_path = ROOT / 'tools/reference/arctic-held-input-client-evidence.json'
    client = e.load(client_path)
    assert client['patches'] == 42
    for group in ('patchHashes', 'sharedInputs'):
        for p, h in client[group].items():
            assert e.sha(ROOT / p) == h, p
    files = e.stage_files(STAGE, WIDE, failed)
    files += [prebuild, earlier, client_path, Path(__file__).resolve(), ROOT / 'tests/check_blade_fury_held_candidate.py',
              ROOT / 'tests/Sanctuary.Integration/BladeFuryHeldCandidateChecks.cs',
              failed / 'BladeFuryHeldCandidateChecks.cs', failed / 'integration/Sanctuary.Integration.dll']
    files += [ROOT / 'docs' / name for name in ('blade-fury-held-input-proposal.md', 'class-skill-roadmap.md',
              'class-skills-resume-checkpoint.md', 'skill-effects.md', 'skill-icons.md')]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, candidateOnly=True, approved=False,
                  productionChanged=False, clientChanged=False, handlerEnabled=False, installed=False,
                  handlers=204, assassinHandlers=24, completedClasses=[], integrationBuild=2, sourceSnapshot=2,
                  focusedMarkers=12, retainedMarkers=231, historicalRetainedMarkers=228,
                  bootsPerCohort=3, reloadsPerCohort=2, savesPerCohort=3, pythonTests=71,
                  rankOne=dict(entryThresholdMana=6, entryDebitMana=0, firstBladeFrame=12,
                               subsequentBladeFrames=5, manaPerBlade=1, steadyManaPerSecond=5,
                               twoSecondBlades=8, twoSecondManaSpent=8),
                  artifacts=inventories[0]['artifacts'], protection=protected, clientPatchesUnchanged=42,
                  scope=[
                      'Test-only explicit input acceptance policy uses unchanged prepared production sequence, resource and weapon-flight services. No learned handler, protocol or client gesture is modified.',
                      'One/three/seven-frame catch-up, duplicate delivery, threshold/exhaustion, early release/fresh windup, actual Tarnhelm rank/cost, aim capture and independent released-blade damage pass.',
                      'Same wounded owner, exact resources and eight original released packets survive two actual reloads without transient mode replay. Future flight inspection deadlines are explicitly supplied.',
                      'All228 historical retained markers remain, including real connected prepared modes, complete source comparisons and36 raw sentry inputs. The source probes are reused unchanged; no new current D2R timing claim.',
                      'The first test expected six-frame cadence; preserved native output demonstrated five frames. Only the expectation changed. Production DLL/kernel/data are byte-identical to the accepted Arctic package.'],
                  limits=[
                      'Input adaptation remains unapproved; Inferno/Arctic approvals do not cover Fury. No gameplay unlock, natural held transport or graphical input acceptance.',
                      'No fresh runtime item audit is claimed: unchanged production DLL and exact source/data comparison reuse the previous accepted runtime. All71Python tests pass with the new fixture.',
                      'Modern AR/mastery/Slow Missiles order, full item/proc/owner/PvP, source geometry and final effects/icons remain unfinished. No class certificate, deployment or overall goal completion.'])
    e.write('blade-fury-held-candidate-native-evidence.json', result, files, check)
    print('Fury review-only:12focused/231retained;3boots/2reloads/3saves each;unchanged production;approval pending;204handlers.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
