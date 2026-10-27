#!/usr/bin/env python3
"""Bind learned Fenris casting and the exact focused/full candidate acceptance."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/dire-wolf-casting'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
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
    assert log.count(': passed') == 230
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('dire-wolf-ai-check: passed') == 1
    assert '3678native ordered-service/memory comparisons' in log
    for name in ['spirit-wolf-mode', 'spirit-wolf-body', 'spirit-wolf-casting', 'barbs-casting', 'barbs-world', 'wolverine-casting', 'oak-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for family in ['spirit-wolf-right', 'spirit-wolf-quick', 'barbs-right', 'barbs-quick', 'oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick']:
        assert log.count(family + '-check: passed') == log.count(family + '-result: passed') == 3, family
    for family in ['spirit-wolf-world-live', 'spirit-wolf-hit-live', 'spirit-wolf-rate-live', 'spirit-wolf-slow-live', 'barbs-world-live', 'oak-world-live', 'wolverine-world-live']:
        assert log.count(family + '-result: passed') == 3, family
    assert 'Learned Dire Wolf SC20mana, rollback/cancellation' in (STAGE / 'stats-run.log').read_text()
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
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    frozen = json.loads(subprocess.check_output(['git', 'show', '67ed09d:tools/reference/dire-wolf-world-native-evidence.json'], cwd=ROOT))
    for name, digest in frozen['artifacts'].items():
        actual = sha(ROOT / name) if name.startswith('.local/') else hashlib.sha256(subprocess.check_output(['git', 'show', '67ed09d:' + name], cwd=ROOT)).hexdigest()
        assert actual == digest, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 171 and sum(x['characterClass'] == 'Druid' for x in handlers) == 15
    assert status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summondirewolf')['implementation'] == 'handler-present'
    saved = json.loads((STAGE / 'dire-wolf-world-fixtures.json').read_text())
    queued, raging = saved['QueuedState']['Controller'], saved['RageState']['Controller']
    assert queued['Mode'] == 4 and queued['Feeding'] and queued['ActionAt'] > queued['Frame']
    assert queued['FeedingCorpse'] == saved['Corpse'] and not queued['Fault']
    assert raging['Mode'] == 4 and not raging['Feeding'] and raging['RagePercent'] == 100
    assert raging['RageUntil'] == raging['Frame'] + 500 and saved['Life'] == 20007 and not raging['Fault']
    assert queued['AttackPacket'] == raging['AttackPacket'] == dict(Rank=1,Minimum=7,Maximum=12,Attack=300)
    assert log.count('dire-wolf-casting-check: passed') == 1 and log.count('dire-wolf-casting-reload-check: passed') == 2
    assert 'cap3at rank4, learned Spirit Wolf/Raven/vine/spirit coexistence and offline rage expiry pass' in log
    for label in ['dire-wolf-right', 'dire-wolf-quick']:
        assert log.count(label + '-check: passed') == log.count(label + '-result: passed') == 3
    cast = json.loads((STAGE / 'dire-wolf-casting-fixtures.json').read_text())
    assert len(cast['Wolves']) == len(cast['States']) == len(cast['Orders']) == 2 and cast['Orders'] == sorted(set(cast['Orders']))
    assert all(json.loads(state)['LearnedCast'] and json.loads(state)['Snapshot']['Birth']['Rank'] == 2 for state in cast['States'])
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'dire-wolf-body-fixtures.json', 'corpse-explosion-cast-fixtures.json', 'dire-wolf-world-fixtures.json']]
    artifacts += [source_path, fixture, ROOT / '.local/class-skills/dire-wolf-ai-source-probe.cpp', ROOT / '.local/class-skills/dire-wolf-ai-sanitizer.log']
    artifacts += [ROOT / name for name in ['src/Sanctuary.Content/DruidDireWolfAi.cs', 'tests/Sanctuary.Integration/DireWolfAiChecks.cs', 'tests/check_dire_wolf_ai.py', 'tools/record_dire_wolf_ai.py', 'tools/probe_dire_wolf_ai_reference.py', 'tools/reference/dire-wolf-corpse-deviation.json', 'src/Sanctuary.Content/DruidDireWolfBody.cs', 'src/Sanctuary.Content/DruidDireWolfCorpses.cs', 'src/Sanctuary.Content/CorpseSkillRules.cs', 'src/Sanctuary.Content/CreatureProfiles.cs', 'tests/Sanctuary.Integration/DireWolfBodyChecks.cs', 'tests/check_dire_wolf_body.py', 'tests/export_runtime_audits.py', 'tools/record_dire_wolf_body.py', 'tools/build_class_skills.py', 'data/Sanctuary/Balance/skill-effects.json']]
    artifacts += [ROOT / name for name in ['src/Sanctuary.Content/DruidDireWolfWorld.cs', 'src/Sanctuary.Content/DruidDireWolfHit.cs', 'src/Sanctuary.Content/DruidNpcSkills.cs', 'src/Sanctuary.Content/BalanceCatalog.cs', 'tests/Sanctuary.Integration/DireWolfWorldChecks.cs', 'tests/check_dire_wolf_world.py', 'tools/probe_dire_wolf_hit_reference.py', 'tools/reference/dire-wolf-hit-source-evidence.json', 'tools/record_dire_wolf_world.py', 'data/Sanctuary/Balance/druid-npc-skills.json', '.local/class-skills/dire-wolf-hit-source-probe.cpp', '.local/class-skills/dire-wolf-hit-source-probe.json', '.local/class-skills/dire-wolf-hit-sanitizer.log']]
    artifacts += [ROOT / name for name in ['src/Sanctuary.Content/DruidDireWolfCasting.cs', 'src/Sanctuary.Content/DruidCombat.cs', 'src/Sanctuary.Content/ClassCombat.cs', 'src/Sanctuary.Content/SkillBindings.cs', 'tests/Sanctuary.Integration/DireWolfCastingChecks.cs', 'tests/check_dire_wolf_casting.py', 'tools/record_dire_wolf_casting.py', 'tools/build_skill_roadmap.py', 'data/Sanctuary/Balance/class-skill-status.json']]
    artifacts.append(STAGE / 'dire-wolf-casting-fixtures.json')
    full_markers = None
    if full_class:
        full = json.loads((STAGE / 'class-inputs.json').read_text())
        assert full['artifacts'] == inputs['artifacts'] and full['copiedData'] == inputs['copiedData']
        for name, digest in full['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log and full_log.count('Shutting down') == 3
        full_markers = full_log.count(': passed')
        assert full_markers >= 1184
        assert 'Native class skills and projectile checks passed, including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
            assert json.loads((STAGE / ('full-' + name)).read_text()) == json.loads((STAGE / name).read_text())
            artifacts.append(STAGE / ('full-' + name))
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    result = dict(referenceVersion='3.3.93847', legacyCommit=source['legacyCommit'], handlerEnabled=True, installed=False,
        handlers=171, druidHandlers=15, completedClasses=[], goalComplete=False,
        verified=dict(sourceContexts=1226, nativeOrderedComparisons=3678, fullClassMarkers=full_markers, rightCasts=3, quickCasts=3, difficultyColumns=3, physicalPacketComparisons=576, naturalFenrisRuns=3, pythonTests=70, namedDecisions=573, namedInputFingerprints=len(audits[0]["InputHashes"]), passMarkers=230, boots=3, retainedReloads=2),
        testedAssemblies=inputs['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Learned right/quick SC casting uses shared wolf target/range/LOS/ground-fit placement. One20mana payment follows successful allocation; failed/null/unsupported allocation or changed payment availability preserves existing paid wolves and FIFO. Current/deleted/self/town target controls and once-only release pass.',
            'Independent Fenris capmin(effective rank,3) uses the existing saved creation-order ledger. Actual Tarnhelm and Battle Command loss remove oldest extras without rewriting retained birth. Actual rank4 caps at3. New casts do not redirect existing wolves. Learned Spirit Wolf,Raven,prepared vine and Oak coexist; Grizzly coexistence awaits its actor.',
            'Unlearned,low fractional mana,stop,out-and-back native movement,map round trip,hit recovery and frozen cancellation refuse unpaid casts. Actual right-hold/release and quick packets cast once at20mana in all three boots. Learned bodies,birth,FIFO and right/quick bindings survive two actual reloads; saved unpaid windups do not replay.',
            'The long offline skip now directly verifies expiry of the500frame rage lease without healing or AI RNG. All prior Fenris policy/body/feeding/rage/physical packet,connected timer and shared summon/corpse/item consumers remain checked.230markers pass across three boots/two reloads,all70Python checks pass,and fresh645/21fingerprints preserve573named decisions. Both builds have zero warnings/errors.',
        ], limits=[
            'This is a learned handler and bounded native acceptance,not class completion or a current-executable parity certificate. The exact full-regression marker count is recorded separately when requested; installation requires its own original-world/preservation receipt.',
            'The learned route currently requires a trained Dire talent. Item-granted/no-hard-point casting,nonzero inherited sunder and NPC equipment delivery remain unsupported in the deferred item-skill scope. Grizzly coexistence needs its actual actor; other existing summon families are checked.',
            'The retained world uses explicit native sector/room/LOS/ground-fit,corpse scan/footprint,path/target-lookup/ties and provisional shared-wolf busy-A1/neutral-delay adapters. Exact modern Fenris feeding/event/cadence,full mode-stat stream and global incoming HP/death/return ordering remain open.',
            'No final art is claimed: silent UO0xE1 Dire Wolf [placeholder],WF movement/bite/feeding/rage/contact/teleport/critical/hurt/death/audio and separate talent/right/quick icons remain pending. Installed main2a6eec9and original saves/config/client remain unchanged until separate rollout.',
        ])
    path = ROOT / 'tools/reference/dire-wolf-casting-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Learned Fenris:230markers/3boots/2reloads,right+quick casts; fullClass={full_markers};171handlers/Druid15,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args=parser.parse_args()
    record(args.check,args.full_class)
