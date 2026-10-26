#!/usr/bin/env python3
"""Bind prepared Creeper arithmetic/AI and saved life-only body acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'creeper'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / directory).glob('*.cs')}
    assert {name for name in inputs['sourceInputs'] if name.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    commands = ['creeper-ai', 'creeper-stats', 'raven-stats', 'raven-flight', 'toward-path', 'valkyrie-creation', 'named-gear', 'first-loop-catalogue']
    for name in commands:
        assert log.count(name + '-check: passed') == 1, name
    assert '3636 complete poison creator and9180 do115/passive source comparisons pass' in log
    assert '255 launch,75 emission,864 complete router and6 sequential history source comparisons pass' in log
    assert '1873complete source contexts replayed across3current difficulty parameter rows,5619native comparisons' in log
    assert log.count('creeper-body-check: passed') == 1
    assert log.count('creeper-body-reload-check: passed') == 2
    assert log.count('Writing world save snapshot done') == 3
    assert log.count('Shutting down') == 3
    assert 'pass3boots/2actual reloads; saved life-only Creeper body checked' in (STAGE / 'stats-run.log').read_text()
    fixture = json.loads((STAGE / 'body-fixtures.json').read_text())
    assert fixture['State']['Profile'] == 'prepared-noRatio-life-and-owner-v1'
    assert fixture['State']['Snapshot']['OwnerLevel'] == 99 and fixture['State']['Snapshot']['RabiesHardRank'] == 1
    assert fixture['State']['Snapshot']['Birth']['Life256'] == 12800
    assert fixture['Maximum'] == 17280 and fixture['Regeneration'] == 12 and fixture['Life'] == 8133
    source = json.loads((ROOT / 'tools/reference/creeper-components-source-evidence.json').read_text())
    assert source['fixture']['damage'] == 7272 and source['fixture']['birth'] == 9185
    assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
    for name, digest in source['generatedSourceHashes'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in source['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    vines = json.loads((ROOT / 'tools/reference/creeper-vines-source-evidence.json').read_text())
    assert sha(ROOT / vines['fixture']['path']) == vines['fixture']['sha256']
    assert sha(BASE / 'creeper-vines-source-probe.cpp') == vines['generatedSourceSha256']
    for name, digest in vines['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    ai = json.loads((ROOT / 'tools/reference/creeper-ai-source-evidence.json').read_text())
    assert ai['fixture']['cases'] == 1873 and sha(ROOT / ai['fixture']['path']) == ai['fixture']['sha256']
    assert sha(BASE / 'creeper-ai-source-probe.cpp') == ai['generatedSourceSha256']
    assert (BASE / 'creeper-ai-sanitizer.log').read_text() == ''
    for name, digest in ai['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit == json.loads((STAGE / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        audits.append(audit)
    baseline = json.loads(subprocess.check_output(['git', 'show', '8cf01d9:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    reference = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((reference / 'sources.json').read_text())['files']
    for table in ['monstats', 'monequip']:
        assert sha(reference / ('json/' + table + '.json')) == manifest['json/' + table + '.json']['sha256']
    monsters = json.loads((reference / 'json/monstats.json').read_text())
    equipment = list(json.loads((reference / 'json/monequip.json').read_text()).values())
    assert monsters['plaguepoppy']['Id'] == monsters['plaguepoppy']['BaseId'] == 'plaguepoppy'
    assert len(equipment) == 45 and not any(row.get('monster') == 'plaguepoppy' for row in equipment)
    equipment_scope = json.loads((ROOT / 'tools/reference/spirit-wolf-equipment-scope.json').read_text())
    for name, digest in equipment_scope['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    guard = equipment_scope['reusedCompiledGuard']
    assert sha(ROOT / guard['probe']) == guard['probeSha256']
    assert json.loads((ROOT / guard['probe']).read_text())['guards'][2] == [2, 0, 0]
    builds = {}
    for name in ['content', 'integration']:
        text = (STAGE / (name + '-build.log')).read_text()
        assert 'Build succeeded.' in text and '0 Error(s)' in text
        builds[name] = int(re.findall(r'(\d+) Warning\(s\)', text)[-1])
        assert builds[name] == 0
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 167 and sum(x['characterClass'] == 'Druid' for x in handlers) == 11
    assert not status['completedClasses']
    paths = [ROOT / p for p in [
        'src/Sanctuary.Content/DruidCreeperStats.cs', 'src/Sanctuary.Content/DruidCreeperVines.cs', 'tests/Sanctuary.Integration/CreeperStatsChecks.cs',
        'tests/check_creeper_stats.py', 'tools/build_class_skills.py', 'tools/probe_creeper_components_reference.py',
        'tools/record_creeper_stats.py', 'tools/reference/creeper-components-source-evidence.json',
        'tools/probe_creeper_vines_reference.py', 'tools/reference/creeper-vines-source-evidence.json',
        'data/Sanctuary/Balance/necromancer-summons.json']]
    paths += [ROOT / p for p in ['src/Sanctuary.Content/DruidCreeperAi.cs', 'tests/Sanctuary.Integration/CreeperAiChecks.cs',
        'tools/probe_creeper_ai_reference.py', 'tools/reference/creeper-ai-source-evidence.json',
        'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    paths += [ROOT / p for p in ['src/Sanctuary.Content/DruidCreeperBody.cs', 'src/Sanctuary.Content/CreatureProfiles.cs',
        'tests/Sanctuary.Integration/CreeperBodyChecks.cs', 'src/Sanctuary.Content/BattleOrders.cs',
        'src/Sanctuary.Content/SummonDifficulties.cs', 'src/Sanctuary.Content/SkeletonPets.cs',
        'data/Sanctuary/Balance/skill-effects.json', 'tools/reference/spirit-wolf-equipment-scope.json',
        'tools/reference/d2r-named-equipment/json/monstats.json', 'tools/reference/d2r-named-equipment/json/monequip.json']]
    paths += [STAGE / p for p in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
                                  'stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'body-fixtures.json']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, handlers=167, druidHandlers=11,
                    completedClasses=[], goalComplete=False, boots=3, actualReloads=2, fullClassRegression=False,
                    buildWarnings=builds, sourceAndNativePoisonCases=3636, sourceAndNativeBirthCases=9180, sourceAndNativeLaunchCases=255, sourceAndNativeEmissionCases=75,
                    sourceAndNativeContactCases=864, sourceAndNativeSequentialCases=6,
                    uniqueSourceAiContexts=1873, difficultyAiReplays=3, nativeAiComparisons=5619, pythonTests=70,
                    namedAuditFingerprints=len(audits[0]['InputHashes']), firstLoopFingerprints=len(audits[1]['InputHashes']), unchangedNamedDefinitions=573,
                    savedLifeOnlyBody=True, lifeOnlyFixtureAutomaticClock=False, learnedHandler=False, noGeneratedEquipmentUnderPinnedDispatch=True,
                    scope='Prepared numeric/outer-AI components and saved vulnerable life/owner body; no world AI, VINE_BEAST state, learned cast, trail or combat acceptance.',
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    established=[
                        'Native source-zero Creeper poison endpoints, hard-only Rabies synergy, signed mastery, length and count match3636 complete creator/getter/writer source cases. Source captures also retain3636 explicit MissileSkill override controls which are not this trail route.',
                        'Native supplied-spawn percentage/explicit-level/passive components match9180 complete do115/passive source cases over all255ranks, three owner levels, all difficulties, inherited poison-sunder controls and fractional fixed-point base-life controls. Level329 is retained without using Raven monster-level lookup. Actual noRatio spawn initialization is a separate composition.',
                        'Actual trained Druid and Tarnhelm exercise effective rank, immutable earlier stats,50-to75life, retained9FP regeneration and explicit75-to76level. Rank-one32/48FP poison over100frames matches the user unallocated tooltip preview12–18over4seconds; no spawned-pet observation is inferred.',
                        'Prepared Vine Attack head-count/cardinal/index requests match255source cases. Remaining-frame emission gates match75cases, full hit50/router/history matches864cases plus6sequential early/alternating/repeated contacts. Modern equivalence is not established; these helpers are not a live field.',
                        'Native Vines outer policy matches1873complete source contexts across three identical current difficulty parameter rows,5619comparisons. Complete ordered requests and remembered attempt frame retain ownerless/town/warp/scan priorities, exact poison-resist100guard, strict elapsed>100and ignored skill-service failure. Target lookup stays lazy and poisoned-state success short-circuits the resistance query. Game-frame reversal and unverified cooldown-sum overflow are refused explicitly.',
                        'Pinned45-row equipment table has no plaguepoppy entry; its identity/base identity match. The already-verified complete loader/negative-index dispatcher and compiled guard imply no generated gear for this species under the current-table/legacy-dispatch model. Other equipped summons retain their separate gaps.',
                        'Saved noRatio body retains50base life,25defense,0base AR,9FP per-frame regeneration, explicit level75at owner99, copied Rabies hard context and immutable effective rank. Actual Tarnhelm fresh capture gives75life/level76without rewriting the old pet. Creature identity is plaguepoppy with footprint0,drain0,chill50,noGH; the existing offensive aura filter excludes isAtt0vines. Direct Orders/Shout and injected Inner Sight are stat-consumer controls, not proof of every cast/target rule.',
                        'Fractional recovery/cap, Orders67.5maximum/12FPregen, offline no-heal, wardrobe/history, unchanged noRatio bases on Hell facet, orphan/owner death and atomic malformed restore pass. Two actual world reloads retain the parked owner/birth/fractional injury/Orders/regen; one active frame adds12FP after each reload, with no offline replay.',
                        'Retained Raven stats/blind/flight, toward paths, Valkyrie creation and named-item checks pass with three body boots/two actual reloads. Builds have zero warnings/errors. Existing installed build, saves, configuration and client are not changed.'
                    ], limits=[
                        'Only an internal saved life/owner body is created. The visible vulnerable silent0x8placeholder does not establish underground VINE_BEAST visibility or general attackability; isAtt0offensive aura exclusion remains intact. Its ordinary native ground-fit factory is a declared preparation adapter, not source zero-size placement. Actual AI/world services, learned summon factory, pet-to-owner Rabies/mastery/pierce transport and poison capture timing remain pending. Source skill delay compares against the global game frame; a future actor must not silently restart a100frame initial cooldown at every summon.',
                        'Trail Range expression is preserved and evaluated as data only; current executable lifetime, head/trail motion/factory/world collision, modern hit-window equivalence and poison delivery are not verified.',
                        'Body-only world reloads are verified; connected skill input, full-class regression, candidate release and installation are not claimed. Fresh item audits preserve573named definitions and all70Python checks pass. Separate final effects/audio and talent/hotkey icon inventories remain placeholders.'
                    ])
    path = ROOT / 'tools/reference/creeper-stats-native-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Prepared Creeper:3boots/2reloads saved life/owner body,14016numeric/vine and5619outer-AI comparisons; world AI/mats/learned casting and installation pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
