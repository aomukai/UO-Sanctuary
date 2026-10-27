#!/usr/bin/env python3
"""Bind complete Fenris outer-policy comparisons to the isolated tested package."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/dire-wolf-ai'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {p for p in inputs['sourceInputs'] if p.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        build = (STAGE / (kind + '-build.log')).read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    source_path = ROOT / 'tools/reference/dire-wolf-ai-source-evidence.json'
    source = json.loads(source_path.read_text())
    fixture = ROOT / source['fixture']['path']
    assert source['referenceVersion'] == '3.3.93847'
    assert source['fixture']['cases'] == 1226 and sha(fixture) == source['fixture']['sha256']
    assert sha(ROOT / '.local/class-skills/dire-wolf-ai-source-probe.cpp') == source['generatedSourceSha256']
    assert (ROOT / '.local/class-skills/dire-wolf-ai-sanitizer.log').read_text() == ''
    for name, digest in source['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / name) == digest, name
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    assert log.count(': passed') == 199
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('dire-wolf-ai-check: passed') == 1
    assert '3678native ordered-service/memory comparisons' in log
    for name in ['spirit-wolf-mode', 'spirit-wolf-body', 'spirit-wolf-casting', 'barbs-casting', 'barbs-world', 'wolverine-casting', 'oak-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for family in ['spirit-wolf-right', 'spirit-wolf-quick', 'barbs-right', 'barbs-quick', 'oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick']:
        assert log.count(family + '-check: passed') == log.count(family + '-result: passed') == 3, family
    for family in ['spirit-wolf-world-live', 'spirit-wolf-hit-live', 'spirit-wolf-rate-live', 'spirit-wolf-slow-live', 'barbs-world-live', 'oak-world-live', 'wolverine-world-live']:
        assert log.count(family + '-result: passed') == 3, family
    assert 'Fenris outer policy: 3678 ordered-service and memory comparisons' in (STAGE / 'stats-run.log').read_text()
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 170 and sum(x['characterClass'] == 'Druid' for x in handlers) == 14
    assert status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summondirewolf')['implementation'] != 'handler-present'
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log']]
    artifacts += [source_path, fixture, ROOT / '.local/class-skills/dire-wolf-ai-source-probe.cpp', ROOT / '.local/class-skills/dire-wolf-ai-sanitizer.log']
    artifacts += [ROOT / name for name in ['src/Sanctuary.Content/DruidDireWolfAi.cs', 'tests/Sanctuary.Integration/DireWolfAiChecks.cs', 'tests/check_dire_wolf_ai.py', 'tools/record_dire_wolf_ai.py', 'tools/probe_dire_wolf_ai_reference.py', 'tools/reference/dire-wolf-corpse-deviation.json']]
    result = dict(referenceVersion='3.3.93847', legacyCommit=source['legacyCommit'], handlerEnabled=False, installed=False,
        handlers=170, druidHandlers=14, completedClasses=[], goalComplete=False,
        verified=dict(sourceContexts=1226, nativeOrderedComparisons=3678, difficultyColumns=3, passMarkers=199, boots=3, retainedReloads=2),
        testedAssemblies=inputs['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Complete Fenris outer policy matches the source fixture in all three current difficulty columns: ordered services, chance consumption, owner lookup/follow/teleport, positive fallback visibility, corpse pursuit, target priority and remembered owner/corpse/pursuit lanes.',
            'Named native memory fields explicitly translate source dwAiParam order (pursuit, corpse GUID, owner GUID). The first test adapter used the wrong order; it was corrected before acceptance. No source case was removed or weakened.',
            'The separate package retains wolf mode, body, normal/haste/Decrepify bites, learned wolf/spirits inputs and actual save/reload checks. Both builds have zero warnings/errors. Installed package preservation was independently rechecked from main.',
        ], limits=[
            'No Dire Wolf body, automatic world controller, actual corpse search/consume/reservation, rage stat consumer, physical damage or learned casting is enabled by this policy component.',
            'Visibility, target and owner lookup, distances, common pet movement, random supply and tactic execution remain explicit services. Legacy target/combat contradictions are literal fixture inputs, not proven reachable modern world states.',
            'Retained reloads and live sessions belong to existing summons. This is no new class completion, current executable parity certificate, full regression rollout, item audit or graphical acceptance.',
            'Approved type-independent monster-corpse edibility supersedes old source material/selectable/dead-collision type filters. Shared ownership/access, one-time reservation and loot preservation still apply; the native consumer remains next.',
            'Final WF movement/bite/corpse/rage/contact/audio assets and separate talent/right/quick icons remain identified placeholders in their existing registers.',
        ])
    path = ROOT / 'tools/reference/dire-wolf-ai-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Fenris policy: 3678 source/native comparisons; 199 markers, 3 boots, 2 retained reloads. Dire actor/corpse/rage/casting still pending; installed build unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
