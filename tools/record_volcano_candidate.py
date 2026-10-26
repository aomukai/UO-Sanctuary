#!/usr/bin/env python3
"""Verify the bounded Volcano native candidate, separately from installation."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'volcano'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(status['skills']) == 240 and not status['completedClasses']
    assert len(present) == 162 and sum(row['characterClass'] == 'Druid' for row in present) == 6
    inventory = json.loads((STAGE / 'focused-inputs.json').read_text())

    def inputs(current):
        for name, digest in current['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        for name, digest in current['copiedData'].items():
            assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
        for name, kind in [('Sanctuary.Content.dll', 'content'), ('Sanctuary.Integration.dll', 'integration')]:
            assert sha(STAGE / kind / name) == current['artifacts']['Assemblies/' + name]

    inputs(inventory)
    log = (STAGE / 'native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    for family in ['volcano', 'twister', 'fissure', 'blizzard', 'molten-boulder', 'firestorm']:
        assert log.count(family + '-check: passed') == 1
        assert log.count(family + '-reload-check: passed') == 2
    for family in ['volcano', 'twister', 'fissure']:
        for mode in ['live', 'input']:
            assert log.count(family + '-' + mode + '-result: passed') == 3
    assert 'pass3boots/2actual reloads/3connected natural actors/3quick casts' in (BASE / 'volcano-focused-run.log').read_text()
    assert 'Volcano:55080complete-source endpoint comparisons/all255ranks pass' in log
    assert 'Volcano:mode0wall/actor passage, actual terminal position and fresh retained-rank lava pass' in log
    full_passes = 0
    if full_class:
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log
        assert 'Native class skills and projectile checks passed' in (BASE / 'volcano-full-run.log').read_text()
        for family in ['volcano', 'twister', 'fissure', 'molten-boulder', 'firestorm']:
            assert full_log.count(family + '-check: passed') == 1
            assert full_log.count(family + '-reload-check: passed') == 2
        for family in ['volcano', 'twister', 'fissure']:
            for mode in ['live', 'input']:
                assert full_log.count(family + '-' + mode + '-result: passed') == 3
        current = json.loads((STAGE / 'class-inputs.json').read_text())
        inputs(current)
        assert current['copiedData'] == inventory['copiedData'] and current['artifacts'] == inventory['artifacts']
        full_passes = full_log.count(': passed')
    fixture = json.loads((STAGE / 'native-fixtures.json').read_text())
    assert {row['Missile'] for row in fixture['State']['Actors']} == {479, 481, 483}
    assert len(fixture['State']['Leases']) == 1 and fixture['State']['DelayScope'] == 'cast-target-provisional-2.7'
    audit = json.loads((ROOT / 'tools/reference/named-item-runtime-audit.json').read_text())
    before = json.loads(subprocess.check_output(['git', 'show', 'b0d171d:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    first = json.loads((ROOT / 'tools/reference/first-loop-affix-audit.json').read_text())
    assert len(audit['Definitions']) == 573 and {row['Id']: row for row in audit['Definitions']} == {row['Id']: row for row in before['Definitions']}
    for table in [audit, first]:
        for name, digest in table['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        assert table['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    paths = [ROOT / name for name in ['src/Sanctuary.Content/DruidVolcano.cs', 'src/Sanctuary.Content/VolcanoPatterns.cs',
             'src/Sanctuary.Content/DruidCombat.cs', 'src/Sanctuary.Content/MissileFootprints.cs', 'src/Sanctuary.Content/SkillBindings.cs',
             'src/Sanctuary.Content/LobProjectiles.cs', 'tests/Sanctuary.Integration/VolcanoChecks.cs', 'tests/check_volcano.py',
             'tests/check_class_skills.py', 'tools/build_class_skills.py', 'tools/record_volcano_candidate.py',
             'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    paths += [ROOT / f'tools/reference/volcano-{suffix}.json' for suffix in ['engine-evidence', 'factory-source-evidence',
              'landing-source-evidence', 'damage-source-evidence', 'damage-evidence', 'pattern-evidence']]
    paths += [STAGE / name for name in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
              'focused-inputs.json', 'native-checks.log', 'native-fixtures.json', 'python-tests.log']]
    paths += [BASE / 'volcano-focused-run.log']
    if full_class:
        paths += [STAGE / 'class-inputs.json', STAGE / 'class-native-checks.log', BASE / 'volcano-full-run.log']
    output = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False, candidateHandlers=162, druidHandlers=6,
                  installedHandlers=161, installedDruidHandlers=5, goalComplete=False, completedClasses=[],
                  focusedBoots=3, actualReloads=2, connectedNaturalActors=3, connectedQuickCasts=3,
                  fullClassRegression=full_class, fullClassPassedMarkers=full_passes, sourceDamageContexts=82620,
                  actualContentEndpointSets=55080, sourceLaunchEmitterChild=5730, sourceFactory=4536, sourceTerminalUpdate=523,
                  planEmitter=906, planSequentialFrames=150, planFactory=216, priorFireBlastLifetimeControls=39601,
                  pythonTests=70, namedAuditFingerprints=len(audit['InputHashes']), firstLoopFingerprints=len(first['InputHashes']),
                  unchangedNamedDefinitions=573, hashes={str(path.relative_to(ROOT)): sha(path) for path in paths},
                  established=[
                      'Learned SC ground/live/deleted/self aim, right-only/quick bindings, once-only25mana release and100-frame local delay pass. Stop/out-and-back/recovery/lost-mana cancellation, early/duplicate release, separate skill cooldown and discarded unpaid windup pass.',
                      'The saved150-frame vent emits62shots at elapsed4,6,...126 using independent persistent emitter memory. Current distance-based1..27-frame mode0 air preserves actual terminal position;18-frame lava captures fresh hard synergies at retained rank. Air does not damage or stop on actors/walls; lava behind caster LOS still damages.',
                      'Stationary Size3/10-frame repeating vent and Size2/LastCollide1/5-frame lava use existing physical/fire, block, recovery/knockback, potion and hit-delay consumers. One collider, history alternation, provisional lease boundaries, separate physical/fire/dual immunity and potion channels pass.',
                      'Actual Tarnhelm, source CanSlow-absent debris speed, preserved physical vent endpoints, shield block/history, blocked-hit one-percent knockback, uninterruptible control and Collision1 vent/Fissure projection/expiry pass.',
                      'Worker serialization is read-only; snapshot arrays do not alias live state and malformed restore is atomic. Offline no-fight expiry, old-facet no-travel aging and class cleanup pass. Exact vent/air/lava pose, endpoints, seeds, clocks, leases, injury and bindings survive two actual world reloads.',
                      'Three connected natural casts and three actual quick packets produce vent, moving air, lava, damage and expiry. Existing Twister/Fissure/Blizzard/Boulder/Firestorm behavior is retained. Full native regression status is recorded above.'
                  ], fixtureControls=[
                      'Source proofs retain explicit row/formula/stat/allocation/path/room/defense/event services; source endpoint sets are separate from native health and input acceptance.',
                      'Held multi-actor state uses a two-day test epoch and existing prevent-heal only after ordinary natural expiry/damage checks. Synthetic direct contacts supply fixed physical/fire endpoints to isolate delivery and history.',
                      'Current native mode0 motion uses the existing Q12 vector/FP16-compatible fractional step adapter. Missing native map room is an explicit edge-removal boundary; no barrier explosion or target snap is invented.'
                  ], remaining=[
                      'Modern cast/target NHD grouping is explicitly provisional; exact current cross-cast/owner/root/lava grouping remains unverified.',
                      'PvP/unmapped/hireling/passive-defense targets, DAMAGE_VS_MONTYPE item layers, full event/ReturnFire/owner/source seed-stream/current-executable and ordinary-client parity remain unsupported or unverified.',
                      'Native geometry, Size3 placement projection, fractional rounding, room/list and timer order remain explicit adapters.',
                      'Final vent/air/lava/explosion/cast/contact/expiry/audio and separate talent/right/quick icons remain identified placeholders; no skill/class parity certificate is claimed.',
                      'Complete full native regression if absent above, then exact-package installation and original-world preservation/rollback acceptance.'
                  ], next='Install only the exact fully tested package with saves/config/client preservation and rollback; then continue Druid Tornado and the complete eight-class240-talent roadmap. Keep Boulder terminal-current-build and Arctic Blast/channel follow-ups individual.')
    path = ROOT / 'tools/reference/volcano-native-evidence.json'
    text = json.dumps(output, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Volcano candidate162/Druid6:3boots/2reloads/3natural/3quick casts,70Python tests and573unchanged named decisions; installation remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
