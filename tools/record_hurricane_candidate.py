#!/usr/bin/env python3
"""Bind Hurricane candidate acceptance to exact native/client artifacts and inputs."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'hurricane'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    coverage = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present = [r for r in coverage['skills'] if r['implementation'] == 'handler-present']
    assert len(coverage['skills']) == 240 and not coverage['completedClasses']
    assert len(present) == 164 and sum(r['characterClass'] == 'Druid' for r in present) == 8

    def inputs(record):
        for name, digest in record['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        for name, digest in record['copiedData'].items():
            assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
        for kind in ['content', 'integration']:
            name = f'Sanctuary.{kind.title()}.dll'
            assert sha(STAGE / kind / name) == record['artifacts']['Assemblies/' + name]

    inventory = json.loads((STAGE / 'focused-inputs.json').read_text())
    inputs(inventory)

    def acceptance(path):
        log = path.read_text()
        assert ' ERR]' not in log and ': failed' not in log
        for family in ['hurricane', 'tornado', 'volcano', 'twister', 'fissure', 'blizzard', 'molten-boulder', 'firestorm']:
            assert log.count(family + '-check: passed') == 1, family
            assert log.count(family + '-reload-check: passed') == 2, family
        for family in ['hurricane', 'tornado', 'volcano', 'twister', 'fissure']:
            for mode in ['live', 'input']:
                assert log.count(family + '-' + mode + '-result: passed') == 3, (family, mode)
        return log.count(': passed')

    acceptance(STAGE / 'native-checks.log')
    assert 'pass3boots/2actual reloads/3connected natural actors/3quick casts' in (BASE / 'hurricane-focused-run.log').read_text()
    log = (STAGE / 'native-checks.log').read_text()
    assert 'Hurricane:9180source endpoint/length comparisons and shared clock/potion controls pass' in log
    assert 'Hurricane:LOS/town/NoAura/retained gear/area shield/potion controls pass' in log
    markers = 0
    if full_class:
        markers = acceptance(STAGE / 'class-native-checks.log')
        assert 'Native class skills and projectile checks passed' in (BASE / 'hurricane-full-run.log').read_text()
        current = json.loads((STAGE / 'class-inputs.json').read_text())
        inputs(current)
        assert current['artifacts'] == inventory['artifacts'] and current['copiedData'] == inventory['copiedData']
    fixture = json.loads((STAGE / 'native-fixtures.json').read_text())
    assert len(fixture['State']) == 1 and fixture['State'][0]['Owner'] == fixture['Owner'] and fixture['PendingMana'] > 0
    audit = json.loads((ROOT / 'tools/reference/named-item-runtime-audit.json').read_text())
    old = json.loads(subprocess.check_output(['git', 'show', '42a90ad:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
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
        assert row['Ability'] == 160 and not row['Left'] and row['Right'] and row['Class'] == 'Druid' and row['Label'] == 'Hurricane'
    assert sha(Path(client['Candidate']['Path'])) == sha(STAGE / 'client/cuo.dll')
    assert sha(Path(client['Baseline']['Path'])) == sha(STAGE / 'client-baseline/cuo.dll')
    builds = {}
    for name in ['content', 'integration', 'client']:
        text = (STAGE / (name + '-build.log')).read_text()
        assert 'Build succeeded.' in text and '0 Error(s)' in text
        builds[name] = int(re.findall(r'(\d+) Warning\(s\)', text)[-1])
    assert builds['content'] == builds['integration'] == 0
    paths = [ROOT / name for name in ['src/Sanctuary.Content/DruidHurricane.cs', 'src/Sanctuary.Content/DruidCombat.cs',
             'src/Sanctuary.Content/DruidMoltenBoulder.cs', 'src/Sanctuary.Content/DruidFirestorm.cs', 'src/Sanctuary.Content/TimedEffects.cs',
             'src/Sanctuary.Content/ColdStates.cs', 'src/Sanctuary.Content/SkillBindings.cs', 'shared/ClassAbilities.cs',
             'src/Sanctuary.Content/ClassDeath.cs', 'src/Sanctuary.Content/CharacterProfiles.cs', 'src/Sanctuary.Content/WardrobeTransaction.cs',
             'tests/Sanctuary.Integration/HurricaneChecks.cs', 'tests/check_hurricane.py', 'tests/check_class_skills.py',
             'tools/build_class_skills.py', 'tools/check_hurricane_damage.py', 'tools/record_hurricane_candidate.py',
             'tools/reference/druid-weather-preflight.json', 'tools/reference/druid-weather-source-evidence.json',
             'tools/reference/hurricane-damage-source-evidence.json', 'tools/reference/hurricane-damage-evidence.json',
             'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE / name for name in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll', 'client/cuo.dll',
              'client-baseline/cuo.dll', 'client-shared.targets', 'client-proof/Program.cs', 'client-proof/check.csproj',
              'client-proof.log', 'content-build.log', 'integration-build.log', 'client-build.log',
              'focused-inputs.json', 'native-checks.log', 'native-fixtures.json', 'python-tests.log', 'native-audits.log']]
    paths += [BASE / 'hurricane-focused-run.log']
    if full_class:
        paths += [STAGE / 'class-inputs.json', STAGE / 'class-native-checks.log', BASE / 'hurricane-full-run.log']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False, candidateHandlers=164, druidHandlers=8,
                    installedHandlers=163, installedDruidHandlers=7, completedClasses=[], goalComplete=False,
                    focusedBoots=3, actualReloads=2, connectedNaturalActors=3, connectedQuickCasts=3,
                    fullClassRegression=full_class, fullClassPassedMarkers=markers, pythonTests=70,
                    sourceWeatherCases=1128, sourceDamageCases=27540, nativeEndpointComparisons=9180,
                    namedAuditFingerprints=len(audit['InputHashes']), firstLoopFingerprints=len(first['InputHashes']),
                    unchangedNamedDefinitions=573, buildWarnings=builds, clientProof=client,
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    established=[
                        'Learned SC self cast, right/quick bindings and one30mana release pass. Low/lost mana, stop/out-and-back/recovery cancellation, duplicate release, no localdelay, lower-rank recast replacement and real Tarnhelm captured rank pass.',
                        'Saved caster-bound weather starts at20frames and repeats every20, with250+50frames per hard Cyclone Armor point. Each pulse reads current hard synergies/mastery/potion at retained cast rank and copies one max-exclusive raw cold roll across recipients.',
                        'Native source-radius/LOS/town/NoAura/NPC metadata controls, shared packet equality, no generic GETHIT request, actual cold states/immunity, shield bypass, actual tier3 potion through immunity, live synergy/gear and caster facet following pass. Other Druid/Blizzard controls are retained.',
                        'Read-only worker serialization, snapshot isolation and atomic malformed restore pass. Exact state/rank/clock/seed/target injury/bindings survive two actual world reloads; unpaid windup is discarded. Offline ages without damage/RNG/replay, expiry removes state and class transitions clear it.',
                        'Matching staged client reflection proves stable ability160/right-only/Druid identity and newly true self targeting. Only cuo.dll differs among staged DLLs also present in the installed client; no graphical playtest or installation is claimed.'
                    ], fixtureControls=[
                        'Declared source row/formula/stat/room/event/RNG services remain bounded; source weather and damage proofs are separate from native health/input evidence.',
                        'Held inspection state starts two days ahead only after separate natural duration/expiry checks; target healing is prevented. On reload the test restores the offline player from its internal parking map to the saved login position before forcing an attack.',
                        'Only named fixture mana recovery is held one hour. The first failed class-transition fixture was in combat; the corrected lifecycle control uses a separate idle player and preserves normal combat locks. Failed logs remain local.'
                    ], remaining=[
                        'Exact modern equal-frame remove-state versus active-event ordering remains unverified; native expiry wins. Global caster RNG interleaving is not reproduced by the saved scoped seed adapter.',
                        'Native source-room/LOS/list/timer/global event/owner/current-executable/client parity, PvP/unmapped/hireling/passive-defense/item monster-type/event/return-fire/shatter branches remain unsupported or unverified. Cold uses existing separately bounded ColdStates consumers.',
                        'Complete full class regression if absent above, then install the exact content/client package with rollback, original-world smoke and save/config/profile/settings preservation. Installed main remains Tornado until separately receipted.',
                        'Final Hurricane wind/debris/cast/pulse/expiry/audio and separate talent/right/quick icons remain identified placeholders; no class parity certificate is claimed.'
                    ], next='Finish full regression, then exact matching content/client installation. Continue Armageddon using shared state setup and distinct delayed impact/ground-fire routes; keep other deferrals individual.')
    out = ROOT / 'tools/reference/hurricane-native-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Hurricane candidate164/Druid8:3boots/2reloads/3natural/3quick casts,70Python tests and matching staged client; installation remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
