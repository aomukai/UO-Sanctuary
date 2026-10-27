#!/usr/bin/env python3
"""Bind saved Fenris world, feeding/rage and physical combat acceptance."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/dire-wolf-world'


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
    assert log.count(': passed') == 214
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('dire-wolf-ai-check: passed') == 1
    assert '3678native ordered-service/memory comparisons' in log
    for name in ['spirit-wolf-mode', 'spirit-wolf-body', 'spirit-wolf-casting', 'barbs-casting', 'barbs-world', 'wolverine-casting', 'oak-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for family in ['spirit-wolf-right', 'spirit-wolf-quick', 'barbs-right', 'barbs-quick', 'oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick']:
        assert log.count(family + '-check: passed') == log.count(family + '-result: passed') == 3, family
    for family in ['spirit-wolf-world-live', 'spirit-wolf-hit-live', 'spirit-wolf-rate-live', 'spirit-wolf-slow-live', 'barbs-world-live', 'oak-world-live', 'wolverine-world-live']:
        assert log.count(family + '-result: passed') == 3, family
    assert 'Fenris saved world, feeding/rage and physical combat verified' in (STAGE / 'stats-run.log').read_text()
    assert log.count('dire-wolf-world-check: passed') == 1
    assert log.count('dire-wolf-world-reload-check: passed') == 2
    assert log.count('dire-wolf-world-live-check: passed') == log.count('dire-wolf-world-live-result: passed') == 3
    assert 'Fenris world:576complete physical/typed source packets' in log
    hitpath = ROOT / 'tools/reference/dire-wolf-hit-source-evidence.json'
    hit = json.loads(hitpath.read_text())
    assert hit['fixture']['cases'] == 576 and sha(ROOT / hit['fixture']['path']) == hit['fixture']['sha256']
    assert sha(ROOT / '.local/class-skills/dire-wolf-hit-source-probe.cpp') == hit['generatedSourceSha256']
    assert (ROOT / '.local/class-skills/dire-wolf-hit-sanitizer.log').read_text() == ''
    for name, digest in hit['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / name) == digest, name
    assert log.count('dire-wolf-body-check: passed') == 1
    assert log.count('dire-wolf-body-reload-check: passed') == 2
    assert log.count('corpse-cast-check: passed') == 1 and log.count('corpse-cast-reload-check: passed') == 2
    assert 'Dire body: saved vulnerable fractional life/54FP recovery' in log
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        audits.append(audit)
    import subprocess
    baseline = json.loads(subprocess.check_output(['git', 'show', '2a6eec9:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    auditlog = (STAGE / 'audit-native-checks.log').read_text()
    assert auditlog.count(': passed') == 2 and 'Error:' not in auditlog and ' ERR]' not in auditlog
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    frozen = json.loads(subprocess.check_output(['git', 'show', '770dca7:tools/reference/dire-wolf-body-native-evidence.json'], cwd=ROOT))
    for name, digest in frozen['artifacts'].items():
        actual = sha(ROOT / name) if name.startswith('.local/') else hashlib.sha256(subprocess.check_output(['git', 'show', '770dca7:' + name], cwd=ROOT)).hexdigest()
        assert actual == digest, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 170 and sum(x['characterClass'] == 'Druid' for x in handlers) == 14
    assert status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summondirewolf')['implementation'] != 'handler-present'
    saved = json.loads((STAGE / 'dire-wolf-world-fixtures.json').read_text())
    queued, raging = saved['QueuedState']['Controller'], saved['RageState']['Controller']
    assert queued['Mode'] == 4 and queued['Feeding'] and queued['ActionAt'] > queued['Frame']
    assert queued['FeedingCorpse'] == saved['Corpse'] and not queued['Fault']
    assert raging['Mode'] == 4 and not raging['Feeding'] and raging['RagePercent'] == 100
    assert raging['RageUntil'] == raging['Frame'] + 500 and saved['Life'] == 20007 and not raging['Fault']
    assert queued['AttackPacket'] == raging['AttackPacket'] == dict(Rank=1,Minimum=7,Maximum=12,Attack=300)
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'audit-native-checks.log', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'dire-wolf-body-fixtures.json', 'corpse-explosion-cast-fixtures.json', 'dire-wolf-world-fixtures.json']]
    artifacts += [source_path, fixture, ROOT / '.local/class-skills/dire-wolf-ai-source-probe.cpp', ROOT / '.local/class-skills/dire-wolf-ai-sanitizer.log']
    artifacts += [ROOT / name for name in ['src/Sanctuary.Content/DruidDireWolfAi.cs', 'tests/Sanctuary.Integration/DireWolfAiChecks.cs', 'tests/check_dire_wolf_ai.py', 'tools/record_dire_wolf_ai.py', 'tools/probe_dire_wolf_ai_reference.py', 'tools/reference/dire-wolf-corpse-deviation.json', 'src/Sanctuary.Content/DruidDireWolfBody.cs', 'src/Sanctuary.Content/DruidDireWolfCorpses.cs', 'src/Sanctuary.Content/CorpseSkillRules.cs', 'src/Sanctuary.Content/CreatureProfiles.cs', 'tests/Sanctuary.Integration/DireWolfBodyChecks.cs', 'tests/check_dire_wolf_body.py', 'tests/export_runtime_audits.py', 'tools/record_dire_wolf_body.py', 'tools/build_class_skills.py', 'data/Sanctuary/Balance/skill-effects.json']]
    artifacts += [ROOT / name for name in ['src/Sanctuary.Content/DruidDireWolfWorld.cs', 'src/Sanctuary.Content/DruidDireWolfHit.cs', 'src/Sanctuary.Content/DruidNpcSkills.cs', 'src/Sanctuary.Content/BalanceCatalog.cs', 'tests/Sanctuary.Integration/DireWolfWorldChecks.cs', 'tests/check_dire_wolf_world.py', 'tools/probe_dire_wolf_hit_reference.py', 'tools/reference/dire-wolf-hit-source-evidence.json', 'tools/record_dire_wolf_world.py', 'data/Sanctuary/Balance/druid-npc-skills.json', '.local/class-skills/dire-wolf-hit-source-probe.cpp', '.local/class-skills/dire-wolf-hit-source-probe.json', '.local/class-skills/dire-wolf-hit-sanitizer.log']]
    result = dict(referenceVersion='3.3.93847', legacyCommit=source['legacyCommit'], handlerEnabled=False, installed=False,
        handlers=170, druidHandlers=14, completedClasses=[], goalComplete=False,
        verified=dict(sourceContexts=1226, nativeOrderedComparisons=3678, difficultyColumns=3, physicalPacketComparisons=576, naturalFenrisRuns=3, pythonTests=70, namedDecisions=573, namedInputFingerprints=len(audits[0]["InputHashes"]), passMarkers=214, boots=3, retainedReloads=2),
        testedAssemblies=inputs['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Saved Fenris world controller uses the accepted complete outer policy with shared ground-fit/path/movement/animation/attack-rate primitives. Distinct visibility, owner range, delayed teleport, corpse pursuit and feeding request dispatch remain Fenris decisions. The player handler is still disabled.',
            'NPC314 imports its own full pinned skill row. A1 feeding waits for its action event, reserves/consumes the selected corpse through the approved type-independent loot-safe route, then installs100damagepercent until frame+500. A consumed-during-windup corpse adds no rage. Frozen-frame processing removes expired rage; offline/load processing retains an active lease while canceling pending actions. No direct resource recovery is invented.',
            'Physical A1 entry captures live owner rank and separate attack-mode endpoints/AR. Immutable birth/passive values remain copied. Grizzly,rage,aura and curse ED aggregate once before the source -90 floor and FP256 damage roll.576complete packet comparisons establish physical/fire/lightning/cold ordering,exclusive ranges,large-value truncation and intrinsic Crit5 doubling. Actual miss,critical,immunity,Enchant,lost range,live gear/next-entry rank and repeated Decrepify bites pass.',
            'Actual world reloads preserve birth,life,corpses and live rage while canceling queued feeding/bites under the existing offline policy. A resumed wolf may approach from its new legal position before beginning a fresh feeding animation. The test checks unchanged target HP and an intact corpse before that new action.',
            'Three connected authenticated loopback sessions use the real40ms timer without manual stepping: the wolf eats once,retains loot,becomes raging,and damages a subsequently spawned monster.214markers pass across three boots/two reloads with all retained wolf/spirits/corpse/item checks.70Python checks and fresh644/21fingerprints preserve573named decisions. Builds have zero warnings/errors. Earlier body acceptance is frozen at770dca7.',
        ], limits=[
            'Learned Dire placement/payment/cancellation/input and capacity enforcement are not yet enabled or accepted. No new handler,completed class,installed rollout or current executable parity certificate is claimed.',
            'Native sector order,size10to five-tile scan,ordinary corpse footprint2,facet-wide room/LOS/ground-fit,path/owner-primary/cached-potential selection and deterministic ties remain explicit native projections. The complete source target-lookup service is not reconstructed by treating the owners cached target as that entire routine.',
            'Offline expiry after a skip longer than the remaining rage lease is implemented but not separately exercised by these reload fixtures. Busy-A1/neutral-delay continuation reuses the provisional shared-wolf policy. The firsthand Decrepify observation was Spirit Wolf; modern Fenris cadence,feeding/animation/event ties,full mode-stat stream and cross-species frame ordering remain unverified.',
            'Current physical floor,extra typed/equipment/sunder/critical hit-class visual flags,PvP/exceptional targets and full incoming HP/death/return ordering retain their separately recorded scope. Existing shared event consumers are reused; this does not close their global chronology gap.',
            'The candidate remains isolated; main2a6eec9and original saves/configuration/client remain preserved. Final WF/corpse/rage/contact/audio and separate talent/right/quick icons remain placeholders.',
        ])
    path = ROOT / 'tools/reference/dire-wolf-world-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Fenris world/rage/physical combat: 214 markers, 3 boots, 2 reloads and 3 natural connected runs; learned casting pending, installed build unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
