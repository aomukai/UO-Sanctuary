#!/usr/bin/env python3
"""Bind prepared Creeper arithmetic acceptance to its exact inputs; no actor claim."""
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
    assert log.count('Shutting down') == 1
    assert 'one disposable boot; no Creeper actor or handler enabled' in (STAGE / 'stats-run.log').read_text()
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
    paths += [STAGE / p for p in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
                                  'stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, handlers=167, druidHandlers=11,
                    completedClasses=[], goalComplete=False, boots=1, actualReloads=0, fullClassRegression=False,
                    buildWarnings=builds, sourceAndNativePoisonCases=3636, sourceAndNativeBirthCases=9180, sourceAndNativeLaunchCases=255, sourceAndNativeEmissionCases=75,
                    sourceAndNativeContactCases=864, sourceAndNativeSequentialCases=6,
                    uniqueSourceAiContexts=1873, difficultyAiReplays=3, nativeAiComparisons=5619, pythonTests=70,
                    namedAuditFingerprints=len(audits[0]['InputHashes']), firstLoopFingerprints=len(audits[1]['InputHashes']), unchangedNamedDefinitions=573,
                    scope='Prepared numeric and outer-AI components through declared services; no Poison Creeper actor, learned cast, trail or combat acceptance.',
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    established=[
                        'Native source-zero Creeper poison endpoints, hard-only Rabies synergy, signed mastery, length and count match3636 complete creator/getter/writer source cases. Source captures also retain3636 explicit MissileSkill override controls which are not this trail route.',
                        'Native supplied-spawn percentage/explicit-level/passive components match9180 complete do115/passive source cases over all255ranks, three owner levels, all difficulties, inherited poison-sunder controls and fractional fixed-point base-life controls. Level329 is retained without using Raven monster-level lookup. Actual noRatio spawn initialization is a separate composition.',
                        'Actual trained Druid and Tarnhelm exercise effective rank, immutable earlier stats,50-to75life, retained9FP regeneration and explicit75-to76level. Rank-one32/48FP poison over100frames matches the user unallocated tooltip preview12–18over4seconds; no spawned-pet observation is inferred.',
                        'Prepared Vine Attack head-count/cardinal/index requests match255source cases. Remaining-frame emission gates match75cases, full hit50/router/history matches864cases plus6sequential early/alternating/repeated contacts. Modern equivalence is not established; these helpers are not a live field.',
                        'Native Vines outer policy matches1873complete source contexts across three identical current difficulty parameter rows,5619comparisons. Complete ordered requests and remembered attempt frame retain ownerless/town/warp/scan priorities, exact poison-resist100guard, strict elapsed>100and ignored skill-service failure. Target lookup stays lazy and poisoned-state success short-circuits the resistance query. Game-frame reversal and unverified cooldown-sum overflow are refused explicitly.',
                        'Retained Raven stats/blind/flight, toward paths, Valkyrie creation and named-item checks pass in one disposable server boot. Builds have zero warnings/errors. Existing installed build, saves, configuration and client are not changed.'
                    ], limits=[
                        'No actor or handler enabled. Actual AI/world services, state attackability, summon factory/full spawn initialization, actual pet-to-owner Rabies/mastery/pierce transport and capture timing remain pending. Source skill delay compares against the global game frame; a future actor must not silently restart a100frame initial cooldown at every summon.',
                        'Trail Range expression is preserved and evaluated as data only; current executable lifetime, head/trail motion/factory/world collision, modern hit-window equivalence and poison delivery are not verified.',
                        'No world reload, connected input, full-class regression, candidate release or installation is claimed. Fresh item audits preserve573named definitions and all70Python checks pass. Separate final effects/audio and talent/hotkey icon inventories remain placeholders.'
                    ])
    path = ROOT / 'tools/reference/creeper-stats-native-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Prepared Creeper components:14016numeric/vine and5619outer-AI comparisons; no actor, handler or installation claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
