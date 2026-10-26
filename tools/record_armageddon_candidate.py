#!/usr/bin/env python3
"""Bind Armageddon acceptance to the exact candidate, reference and client."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'armageddon'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    coverage = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present = [r for r in coverage['skills'] if r['implementation'] == 'handler-present']
    assert len(coverage['skills']) == 240 and not coverage['completedClasses']
    assert len(present) == 165 and sum(r['characterClass'] == 'Druid' for r in present) == 9

    def inputs(inventory):
        for name, digest in inventory['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        for name, digest in inventory['copiedData'].items():
            assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
        for kind in ['content', 'integration']:
            name = f'Sanctuary.{kind.title()}.dll'
            assert sha(STAGE / kind / name) == inventory['artifacts']['Assemblies/' + name]

    inventory = json.loads((STAGE / 'focused-inputs.json').read_text())
    inputs(inventory)

    def acceptance(path):
        log = path.read_text()
        assert ' ERR]' not in log and ': failed' not in log
        for family in ['armageddon', 'hurricane', 'tornado', 'volcano', 'twister', 'fissure', 'blizzard', 'molten-boulder', 'firestorm']:
            assert log.count(family + '-check: passed') == 1, family
            assert log.count(family + '-reload-check: passed') == 2, family
        for family in ['armageddon', 'hurricane', 'tornado', 'volcano', 'twister', 'fissure']:
            for mode in ['live', 'input']:
                assert log.count(family + '-' + mode + '-result: passed') == 3, (family, mode)
        assert log.count('Armageddon:55080complete-source endpoint comparisons pass') == 3
        assert log.count('Armageddon:720source emitter memory/coordinate/seed comparisons pass') == 1
        return log.count(': passed')

    acceptance(STAGE / 'native-checks.log')
    assert 'pass3boots/2actual reloads/3connected natural actors/3quick casts' in (BASE / 'armageddon-focused-run.log').read_text()
    markers = 0
    if full_class:
        markers = acceptance(STAGE / 'class-native-checks.log')
        assert 'Native class skills and projectile checks passed' in (BASE / 'armageddon-full-run.log').read_text()
        current = json.loads((STAGE / 'class-inputs.json').read_text())
        inputs(current)
        assert current['artifacts'] == inventory['artifacts'] and current['copiedData'] == inventory['copiedData']
    fixture = json.loads((STAGE / 'native-fixtures.json').read_text())
    state = fixture['State']
    assert len(state['Weather']) == 1 and state['Weather'][0]['Owner'] == fixture['Owner']
    assert {x['Missile'] for x in state['Actors']} == {577, 623} and fixture['PendingMana'] > 0
    audit = json.loads((ROOT / 'tools/reference/named-item-runtime-audit.json').read_text())
    old = json.loads(subprocess.check_output(['git', 'show', 'db55ba5:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    first = json.loads((ROOT / 'tools/reference/first-loop-affix-audit.json').read_text())
    assert len(audit['Definitions']) == 573 and audit['Definitions'] == old['Definitions']
    for table in [audit, first]:
        assert table['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in table['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    client = json.loads((STAGE / 'client-proof.log').read_text())
    assert client['Result'] == 'passed' and not client['Baseline']['SelfTarget'] and client['Candidate']['SelfTarget']
    for row in [client['Baseline'], client['Candidate']]:
        assert row['Ability'] == 161 and not row['Left'] and row['Right'] and row['Class'] == 'Druid' and row['Label'] == 'Armageddon'
    assert sha(Path(client['Candidate']['Path'])) == sha(STAGE / 'client/cuo.dll')
    assert sha(Path(client['Baseline']['Path'])) == sha(STAGE / 'client-baseline/cuo.dll')
    builds = {}
    for name in ['content', 'integration', 'client']:
        text = (STAGE / (name + '-build.log')).read_text()
        assert 'Build succeeded.' in text and '0 Error(s)' in text
        builds[name] = int(re.findall(r'(\d+) Warning\(s\)', text)[-1])
    assert builds['content'] == builds['integration'] == 0
    names = ['src/Sanctuary.Content/DruidArmageddon.cs', 'src/Sanctuary.Content/DruidWeather.cs',
             'src/Sanctuary.Content/DruidHurricane.cs', 'src/Sanctuary.Content/DruidTornado.cs',
             'src/Sanctuary.Content/DruidCombat.cs', 'src/Sanctuary.Content/MissileFootprints.cs',
             'src/Sanctuary.Content/SkillBindings.cs', 'shared/ClassAbilities.cs',
             'src/Sanctuary.Content/ClassDeath.cs', 'src/Sanctuary.Content/CharacterProfiles.cs',
             'src/Sanctuary.Content/WardrobeTransaction.cs', 'tests/Sanctuary.Integration/ArmageddonChecks.cs',
             'tests/check_armageddon.py', 'tests/check_class_skills.py', 'tools/build_class_skills.py',
             'tools/check_armageddon_damage.py', 'tools/record_armageddon_candidate.py',
             'tools/reference/druid-weather-preflight.json', 'tools/reference/druid-weather-source-evidence.json',
             'tools/reference/armageddon-damage-evidence.json', 'tools/reference/named-item-runtime-audit.json',
             'tools/reference/first-loop-affix-audit.json']
    names += ['tools/reference/armageddon-' + part + '-source-evidence.json'
              for part in ['damage', 'emitter', 'terminal', 'factory', 'ground']]
    paths = [ROOT / name for name in names]
    paths += [STAGE / name for name in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
              'client/cuo.dll', 'client-baseline/cuo.dll', 'client-shared.targets', 'client-proof/Program.cs',
              'client-proof/check.csproj', 'client-proof.log', 'content-build.log', 'integration-build.log',
              'client-build.log', 'focused-inputs.json', 'native-checks.log', 'native-fixtures.json',
              'python-tests.log', 'native-audits.log']]
    paths += [BASE / 'armageddon-focused-run.log']
    if full_class:
        paths += [STAGE / 'class-inputs.json', STAGE / 'class-native-checks.log', BASE / 'armageddon-full-run.log']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False,
                    candidateHandlers=165, druidHandlers=9, installedHandlers=164, installedDruidHandlers=8,
                    completedClasses=[], goalComplete=False, focusedBoots=3, actualReloads=2,
                    connectedNaturalActors=3, connectedQuickCasts=3, fullClassRegression=full_class,
                    fullClassPassedMarkers=markers, pythonTests=70, nativeEndpointComparisons=55080,
                    nativeEmitterComparisons=720, namedAuditFingerprints=len(audit['InputHashes']),
                    firstLoopFingerprints=len(first['InputHashes']), unchangedNamedDefinitions=573,
                    buildWarnings=builds, clientProof=client, hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    established=[
                        'Learned SC self/right/quick cast pays35mana once at release. Low/lost mana, stop/out-and-back/recovery cancellation, duplicate release, no localdelay, real Tarnhelm and lower-rank recast replacement pass. Hurricane remains independent.',
                        'Weather captures rank and250+50frames per hard Fissure point. Each6-frame emission reproduces source saved memory, coordinate attempts and final seed; current hard synergies/gear are captured at retained rank into a separate stationary25-frame control.',
                        'No contact damage precedes terminal expiry. One physical/fire packet is copied across source-radius recipients, with get-hit, immunity and approved potion controls. A fresh current-owner own-row fire packet starts a37-frame child with36single-collider damage opportunities, DamageRate41 mitigation, real footprint and no ground get-hit.',
                        'Native walls/town, captured slow payload, old-facet/offline aging without fighting and class cleanup pass. Read-only worker serialization, snapshot isolation and atomic malformed restore pass. Exact weather/control/field/clock/seed/injury/bindings survive two actual reloads; unpaid windup is discarded.',
                        'Connected natural lifetimes and actual quick packets pass on three boots. Retained Hurricane/Tornado/Volcano/Twister/Fissure/Blizzard/Boulder/Firestorm checks pass. Matching staged client reflection proves stable ability161/right-only/Druid identity with new self targeting.'
                    ], fixtureControls=[
                        'Declared source services and older-engine routine proofs are bounded evidence, separate from native health/input acceptance and modern executable parity.',
                        'Initial casting refusal was caused by placing the fixture in Heartwood town. Outdoor scenes now start at y800 and assert eligibility; gameplay town restrictions were preserved.',
                        'Natural casts advance the shared actor allocator after the held manifest is created. Their cleanup checkpoints the new watermark only after asserting exact held weather/actor equality; reload must restore that exact checkpointed watermark.',
                        'Held inspection state starts two days ahead after separate natural expiry controls. Healing and mana regeneration are frozen only for named fixtures; reload restores the offline player from internal parking before forced damage.'
                    ], remaining=[
                        'Modern equal-frame expiry/event ordering remains uncertain; native expiry wins. Scoped saved RNG does not establish global caster stream interleaving.',
                        'Exact native source-room/radial geometry/list/timer/global event/current-executable/client parity remains open. PvP/unmapped/hireling/passive-defense/item monster-type/event/return-fire/shatter branches remain unsupported or unverified.',
                        'Complete full regression if absent above, then install the exact content/client package with rollback and original-world preservation. Installed main remains Hurricane until separately receipted.',
                        'Final cast/fall/impact/burning-ground/expiry/audio effects and separate talent/right/quick icons remain identified placeholders. No complete skill/class parity certificate is claimed.'
                    ], next='Finish full acceptance and exact-package installation, then continue unaffected Druid skills while keeping individual blockers explicit.')
    out = ROOT / 'tools/reference/armageddon-native-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Armageddon candidate165/Druid9:3boots/2reloads/3natural/3quick casts,70Python tests and matching staged client; installation remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
