#!/usr/bin/env python3
"""Record exact Tornado candidate acceptance, separately from installation."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'tornado'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    coverage = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present = [row for row in coverage['skills'] if row['implementation'] == 'handler-present']
    assert len(coverage['skills']) == 240 and not coverage['completedClasses']
    assert len(present) == 163 and sum(row['characterClass'] == 'Druid' for row in present) == 7
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
    for family in ['tornado', 'volcano', 'twister', 'fissure', 'blizzard', 'molten-boulder', 'firestorm']:
        assert log.count(family + '-check: passed') == 1
        assert log.count(family + '-reload-check: passed') == 2
    for family in ['tornado', 'volcano', 'twister', 'fissure']:
        for mode in ['live', 'input']:
            assert log.count(family + '-' + mode + '-result: passed') == 3
    assert 'pass3boots/2actual reloads/3connected natural actors/3quick casts' in (BASE / 'tornado-focused-run.log').read_text()
    assert 'Tornado:27540source endpoints/200complete paths/243area-radius comparisons pass' in log
    assert 'Tornado:area/direct/shield/history/NOAURA/potion/gear/slow/knockback/wall controls pass' in log
    markers = 0
    if full_class:
        full = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full and ': failed' not in full
        assert 'Native class skills and projectile checks passed' in (BASE / 'tornado-full-run.log').read_text()
        for family in ['tornado', 'volcano', 'twister', 'fissure', 'molten-boulder', 'firestorm']:
            assert full.count(family + '-check: passed') == 1 and full.count(family + '-reload-check: passed') == 2
        for family in ['tornado', 'volcano', 'twister', 'fissure']:
            for mode in ['live', 'input']:
                assert full.count(family + '-' + mode + '-result: passed') == 3
        current = json.loads((STAGE / 'class-inputs.json').read_text())
        inputs(current)
        assert current['copiedData'] == inventory['copiedData'] and current['artifacts'] == inventory['artifacts']
        markers = full.count(': passed')
    fixture = json.loads((STAGE / 'native-fixtures.json').read_text())
    assert len(fixture['State']['Missiles']) == 1 and len(fixture['State']['Leases']) == 1
    assert fixture['State']['DelayScope'] == 'cast-target-provisional-2.7'
    assert len(fixture['State']['Missiles'][0]['Path']) == 38 and fixture['State']['Missiles'][0]['Steps']['Cells']
    assert fixture['State']['Missiles'][0]['LastTarget'] == fixture['Target']
    audit = json.loads((ROOT / 'tools/reference/named-item-runtime-audit.json').read_text())
    before = json.loads(subprocess.check_output(['git', 'show', 'c815e91:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    first = json.loads((ROOT / 'tools/reference/first-loop-affix-audit.json').read_text())
    assert len(audit['Definitions']) == 573 and {row['Id']: row for row in audit['Definitions']} == {row['Id']: row for row in before['Definitions']}
    for table in [audit, first]:
        for name, digest in table['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        assert table['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    paths = [ROOT / name for name in ['src/Sanctuary.Content/DruidTornado.cs', 'src/Sanctuary.Content/DruidCombat.cs',
             'src/Sanctuary.Content/ChargedBolts.cs', 'src/Sanctuary.Content/DruidEmitterPatterns.cs', 'src/Sanctuary.Content/SourceMissileSteps.cs',
             'src/Sanctuary.Content/DruidTwister.cs', 'src/Sanctuary.Content/DruidMoltenBoulder.cs',
             'tests/Sanctuary.Integration/TornadoChecks.cs', 'tests/check_tornado.py', 'tests/check_class_skills.py',
             'tools/build_class_skills.py', 'tools/record_tornado_candidate.py', 'tools/reference/named-item-runtime-audit.json',
             'tools/reference/first-loop-affix-audit.json']]
    paths += [ROOT / f'tools/reference/tornado-{suffix}.json' for suffix in ['preflight', 'pulse-source-evidence', 'area-source-evidence',
              'path-source-evidence', 'damage-source-evidence', 'damage-evidence']]
    paths += [STAGE / name for name in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
              'focused-inputs.json', 'native-checks.log', 'native-fixtures.json', 'python-tests.log']]
    paths += [BASE / 'tornado-focused-run.log']
    if full_class:
        paths += [STAGE / 'class-inputs.json', STAGE / 'class-native-checks.log', BASE / 'tornado-full-run.log']
    output = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False, candidateHandlers=163, druidHandlers=7,
                  installedHandlers=162, installedDruidHandlers=6, completedClasses=[], goalComplete=False,
                  focusedBoots=3, actualReloads=2, connectedNaturalActors=3, connectedQuickCasts=3,
                  fullClassRegression=full_class, fullClassPassedMarkers=markers, sourceEndpointComparisons=27540,
                  completePathComparisons=200, sourceAreaCases=17496, sourcePulseCompositionCases=2463, nativeRadiusComparisons=243,
                  pythonTests=70, namedAuditFingerprints=len(audit['InputHashes']), firstLoopFingerprints=len(first['InputHashes']),
                  unchangedNamedDefinitions=573, hashes={str(path.relative_to(ROOT)): sha(path) for path in paths},
                  established=[
                      'Learned SC ground/live/deleted/self aim, L/R/quick bindings, once-only10mana release and no local delay pass. Early/duplicate release, stop/out-and-back/recovery/lost-mana cancellation and discarded unpaid windup pass.',
                      'One75-frame charged flight captures physical endpoints,38source waypoints and37path RNG draws. Five periodic phases0/15/30/45/60 precede movement; periodic copies share one raw roll while separate Size3 direct contacts retain LastCollide and provisional25-frame cast/target delay.',
                      'Native pulse/direct double delivery, area shield bypass versus shielded direct damage, pulse bypass of direct history/leases, one shared packet across victims, terminal-update ordering and no extra stun pass.',
                      'Exact NOAURA/NPC metadata controls, inclusive center-radius geometry, physical immunity/approved potion channels, actual Tarnhelm, captured Slow Missiles velocity with preserved physical endpoints, blocked direct one-percent knockback and actual wall barrier pass.',
                      'Worker serialization is read-only; path/step snapshots are deep copies and malformed restore is atomic. Offline no-damage expiry, old-facet no-travel aging and class cleanup pass. Exact saved path/pose/steps/seed/clock/history/lease/injury/bindings survive two actual world reloads.',
                      'Three natural connected flights and three actual quick packets move, damage and expire. Existing Volcano/Twister/Fissure/Blizzard/Boulder/Firestorm behavior is retained; full class regression status is recorded above.'
                  ], fixtureControls=[
                      'Source formulas/rows/stat/room/hostility/defense/event services remain bounded. Endpoint, path and area-source comparisons are distinct from native health/input acceptance.',
                      'Controlled low velocity15 isolates all five pulse phases; a controlled valid final-waypoint pose tests pulse-before-terminal order. These are component controls rather than claims about ordinary player trajectories.',
                      'Held state uses a two-day test epoch and existing prevent-heal only after normal natural lifetime/damage checks. Direct/pulse delivery uses fixed raw1536 or declared256..512 endpoints to isolate routing and shared rolls.'
                  ], remaining=[
                      'Modern periodic/direct overlap and cross-cast/owner NHD identity remain unverified; direct cast/target25-frame grouping is explicitly provisional.',
                      'PvP/unmapped/hireling/passive-defense targets, monster-type item layers, complete event/ReturnFire/owner/current-executable/client parity remain unsupported or unverified. Native charged interpolation/rounding/room/list/timer and hostility/visibility are explicit adapters.',
                      'Current-row factory, generic dispatcher and broader source/global scheduling are supported by reused bounded proofs, not a modern executable certificate.',
                      'Final travelling wind, cast/direct/periodic/expiry/audio and separate talent/L/R/quick icons remain identified placeholders; no skill/class parity certificate is claimed.',
                      'Complete full native regression if absent above, then exact-package installation and original-world preservation/rollback acceptance.'
                  ], next='Finish full regression if needed, then install only the exact tested package with preservation/rollback. Continue remaining Druid weather and summons; keep Arctic Blast/channel and Boulder terminal correction individual.')
    out = ROOT / 'tools/reference/tornado-native-evidence.json'
    text = json.dumps(output, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Tornado candidate163/Druid7:3boots/2reloads/3natural/3quick casts,70Python tests and573unchanged named decisions; installation remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
