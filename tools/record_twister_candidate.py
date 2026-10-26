#!/usr/bin/env python3
"""Record the bounded Twister candidate; installation requires separate acceptance."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'twister'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    coverage = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present = [s for s in coverage['skills'] if s['implementation'] == 'handler-present']
    assert len(coverage['skills']) == 240 and not coverage['completedClasses']
    assert len(present) == 161 and sum(s['characterClass'] == 'Druid' for s in present) == 5
    inventory = json.loads((STAGE / 'focused-inputs.json').read_text())
    for name, digest in inventory['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inventory['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for name, kind in [('Sanctuary.Content.dll', 'content'), ('Sanctuary.Integration.dll', 'integration')]:
        assert sha(STAGE / kind / name) == inventory['artifacts']['Assemblies/' + name]
    log = (STAGE / 'native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    for family in ['twister', 'fissure', 'blizzard', 'molten-boulder', 'firestorm']:
        assert log.count(family + '-check: passed') == 1
        assert log.count(family + '-reload-check: passed') == 2
    for result in ['twister-live-result', 'twister-input-result', 'fissure-live-result', 'fissure-input-result']:
        assert log.count(result + ': passed') == 3
    assert log.count('blizzard-result: passed') == 1 and log.count('blizzard-reload-result: passed') == 2
    assert 'pass3boots/2actual reloads/3connected natural actors/3quick casts' in (BASE / 'twister-focused-run.log').read_text()
    assert 'Twister:actualTarnhelm/SlowCapture/potion/blocked-knockback/wall controls pass' in log
    assert 'Twister:9180source endpoints/600complete paths/11200flag-health-stun-mode controls pass' in log
    if full_class:
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log
        assert 'Native class skills and projectile checks passed' in (BASE / 'twister-full-run.log').read_text()
        for family in ['twister', 'fissure', 'molten-boulder', 'firestorm']:
            assert full_log.count(family + '-check: passed') == 1
            assert full_log.count(family + '-reload-check: passed') == 2
        for result in ['twister-live-result', 'twister-input-result', 'fissure-live-result', 'fissure-input-result']:
            assert full_log.count(result + ': passed') == 3
        full_inputs = json.loads((STAGE / 'class-inputs.json').read_text())
        for name, digest in full_inputs['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        assert full_inputs['copiedData'] == inventory['copiedData']
        assert full_inputs['artifacts'] == inventory['artifacts']
    fixture = json.loads((STAGE / 'native-fixtures.json').read_text())
    assert len(fixture['State']['Missiles']) == 3 and len(fixture['State']['Leases']) == 1
    assert fixture['State']['DelayScope'] == 'cast-target-provisional-2.7'
    assert all(s['Path'] and s['Steps']['Cells'] for s in fixture['State']['Missiles'])
    audit = json.loads((ROOT / 'tools/reference/named-item-runtime-audit.json').read_text())
    before = json.loads((STAGE / 'pre-native-named-audit.json').read_text())
    first = json.loads((ROOT / 'tools/reference/first-loop-affix-audit.json').read_text())
    assert {r['Id']: r for r in audit['Definitions']} == {r['Id']: r for r in before['Definitions']}
    assert len(audit['Definitions']) == 573
    for table in [audit, first]:
        for name, digest in table['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        assert table['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
    tests = (BASE / 'twister-python-tests.log').read_text()
    assert 'Ran 70 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    paths = [ROOT / p for p in ['src/Sanctuary.Content/DruidTwister.cs', 'src/Sanctuary.Content/DruidCombat.cs',
             'src/Sanctuary.Content/DruidFirestorm.cs', 'src/Sanctuary.Content/DruidMoltenBoulder.cs',
             'src/Sanctuary.Content/DruidEmitterPatterns.cs', 'src/Sanctuary.Content/SourceMissileSteps.cs',
             'src/Sanctuary.Content/CombatStuns.cs', 'tests/Sanctuary.Integration/TwisterChecks.cs', 'tests/check_twister.py',
             'tools/record_twister_candidate.py', 'tools/reference/twister-source-evidence.json',
             'tools/reference/twister-damage-source-evidence.json', 'tools/reference/twister-damage-evidence.json',
             'tools/reference/twister-impact-source-evidence.json', 'tools/probe_twister_path_reference.py',
             'tools/reference/twister-path-source-evidence.json', 'tests/check_class_skills.py', 'tools/reference/named-item-runtime-audit.json',
             'tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE / p for p in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
              'focused-inputs.json', 'native-checks.log', 'native-fixtures.json', 'pre-native-named-audit.json', 'audit-refresh-native.log']]
    paths += [BASE / p for p in ['twister-focused-run.log', 'twister-python-tests.log', 'twister-damage-run.log']]
    if full_class:
        paths += [STAGE / 'class-inputs.json', STAGE / 'class-native-checks.log', BASE / 'twister-full-run.log']
    evidence = dict(referenceVersion='3.3.93847', installed=False, handlerEnabled=True, candidateHandlers=161, druidHandlers=5,
                    installedHandlers=160, installedDruidHandlers=4, goalComplete=False, completedClasses=[],
                    focusedBoots=3, actualReloads=2, connectedNaturalActors=3, connectedQuickCasts=3,
                    fullClassRegression=full_class, sourceEndpointComparisons=9180, reactionAndStunFrameComparisons=11200, completePathComparisons=600,
                    pythonTests=70, namedAuditFingerprints=len(audit['InputHashes']), firstLoopFingerprints=len(first['InputHashes']),
                    unchangedNamedDefinitions=573, hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    established=['Learned SC ground/live/deleted/self aim, L/R/quick binding, once-only7mana release, no added local delay, stop/movement/recovery/lost-mana cancellation and discarded unpaid windup pass.',
                                 'Three seeded50-frame charged flights retain captured physical endpoints and the path-building RNG advances before contact. Retained source steps preserve collision during subcell movement. Source flag/knockback and stun-duration kernels are compared separately from native health/stun cases.',
                                 'Native ordinary-monster physical damage, shield block, physical immunity with stun, live hard Arctic Blast duration, independent captured damage versus later hard-point changes and explicit25-frame lease boundary pass.',
                                 'Actual Tarnhelm effective rank/captured physical endpoints, Slow Missiles velocity with preserved physical endpoints, real tier3 approved potion channels through physical immunity, blocked-hit one-percent knockback and an actual wall barrier pass. Complete50-frame source initialization/path/final seed compares600cases; full source mode priority compares11200contexts.',
                                 'Worker serialization is read-only; path snapshots do not alias live state and malformed restores are atomic. Offline/old-facet expiry, class-history cleanup and exact saved path/step/seed/clock/lease/stun/fractional injury/bindings pass.',
                                 'Three connected prepared flight runs and three real quick packets move, damage, stun and expire naturally. Retained Fissure/Boulder/Firestorm and actual Blizzard damage/lifetime/reload controls pass.'],
                    fixtureControls=['The held state has a two-day test epoch and real existing prevent-heal state only after ordinary natural lifetime/damage checks. Offline held ownership is not used to claim natural damage.',
                                     'Deterministic native direct-hit cases supply fixed1536 endpoints. Source matrix health includes an explicit hireling resistance branch; there is no native hireling actor certificate.',
                                     'Shared terrain and approved potion helpers are reused through accessibility-only changes; full class regression status is recorded above; installation acceptance remains separate.'],
                    remaining=['Modern cast/target25-frame NHD grouping is explicitly provisional; precise current cross-cast/owner grouping is unverified.',
                               'Complete full class regression if not recorded above, then exact-package installation and existing-world preservation acceptance.',
                               'PvP, unmapped/hireling/passive-defense targets, DAMAGE_VS_MONTYPE layers, full event/ReturnFire/owner/current-executable and ordinary-client acceptance remain unsupported or unverified. Native geometry/rounding/room/timer order remains an adapter.',
                               'Final wind/cast/contact/stun/expiry/audio and separate talent/left/right/quick icons remain identified placeholders. No skill/class parity certificate is claimed.'],
                    next='Finish full regression if not recorded above, then exact-package installation with preservation/rollback. Preserve the installed Fissure world and continue the full eight-class240-talent roadmap.')
    text = json.dumps(evidence, indent=2) + '\n'
    path = ROOT / 'tools/reference/twister-native-evidence.json'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Twister candidate161/Druid5:3boots/2reloads/3natural actors/3quick casts,70Python tests,573unchanged named decisions; installed Fissure remains160/Druid4.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
