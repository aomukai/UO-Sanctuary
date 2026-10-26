#!/usr/bin/env python3
"""Bind prepared Creeper arithmetic acceptance to its exact inputs; no actor claim."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'creeper'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    commands = ['creeper-stats', 'raven-stats', 'raven-flight', 'toward-path', 'valkyrie-creation', 'named-gear']
    for name in commands:
        assert log.count(name + '-check: passed') == 1, name
    assert '3636 complete poison creator and9180 do115/passive source comparisons pass' in log
    assert '255 launch,75 emission,864 complete router and6 sequential history source comparisons pass' in log
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
    builds = {}
    for name in ['content', 'integration']:
        text = (STAGE / (name + '-build.log')).read_text()
        assert 'Build succeeded.' in text and '0 Error(s)' in text
        builds[name] = int(re.findall(r'(\d+) Warning\(s\)', text)[-1])
        assert builds[name] == 0
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 166 and sum(x['characterClass'] == 'Druid' for x in handlers) == 10
    assert not status['completedClasses']
    paths = [ROOT / p for p in [
        'src/Sanctuary.Content/DruidCreeperStats.cs', 'src/Sanctuary.Content/DruidCreeperVines.cs', 'tests/Sanctuary.Integration/CreeperStatsChecks.cs',
        'tests/check_creeper_stats.py', 'tools/build_class_skills.py', 'tools/probe_creeper_components_reference.py',
        'tools/record_creeper_stats.py', 'tools/reference/creeper-components-source-evidence.json',
        'tools/probe_creeper_vines_reference.py', 'tools/reference/creeper-vines-source-evidence.json',
        'data/Sanctuary/Balance/necromancer-summons.json']]
    paths += [STAGE / p for p in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
                                  'stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, handlers=166, druidHandlers=10,
                    completedClasses=[], goalComplete=False, boots=1, actualReloads=0, fullClassRegression=False,
                    buildWarnings=builds, sourceAndNativePoisonCases=3636, sourceAndNativeBirthCases=9180, sourceAndNativeLaunchCases=255, sourceAndNativeEmissionCases=75,
                    sourceAndNativeContactCases=864, sourceAndNativeSequentialCases=6,
                    scope='Prepared numeric components only; no Poison Creeper actor, learned cast, trail or combat acceptance.',
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    established=[
                        'Native source-zero Creeper poison endpoints, hard-only Rabies synergy, signed mastery, length and count match3636 complete creator/getter/writer source cases. Source captures also retain3636 explicit MissileSkill override controls which are not this trail route.',
                        'Native supplied-spawn percentage/explicit-level/passive components match9180 complete do115/passive source cases over all255ranks, three owner levels, all difficulties, inherited poison-sunder controls and fractional fixed-point base-life controls. Level329 is retained without using Raven monster-level lookup. Actual noRatio spawn initialization is a separate composition.',
                        'Actual trained Druid and Tarnhelm exercise effective rank, immutable earlier stats,50-to75life, retained9FP regeneration and explicit75-to76level. Rank-one32/48FP poison over100frames matches the user unallocated tooltip preview12–18over4seconds; no spawned-pet observation is inferred.',
                        'Prepared Vine Attack head-count/cardinal/index requests match255source cases. Remaining-frame emission gates match75cases, full hit50/router/history matches864cases plus6sequential early/alternating/repeated contacts. Modern equivalence is not established; these helpers are not a live field.',
                        'Retained Raven stats/blind/flight, toward paths, Valkyrie creation and named-item checks pass in one disposable server boot. Builds have zero warnings/errors. Existing installed build, saves, configuration and client are not changed.'
                    ], limits=[
                        'No actor or handler enabled. AI, state attackability, summon factory/full spawn initialization, actual pet-to-owner Rabies/mastery/pierce transport and capture timing remain pending.',
                        'Trail Range expression is preserved and evaluated as data only; current executable lifetime, head/trail motion/factory/world collision, modern hit-window equivalence and poison delivery are not verified.',
                        'No world reload, connected input, full-class regression, audit refresh, candidate release or installation is claimed. Separate final effects/audio and talent/hotkey icon inventories remain placeholders.'
                    ])
    path = ROOT / 'tools/reference/creeper-stats-native-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Prepared Creeper components:14016 source/native comparisons; no actor, handler or installation claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
