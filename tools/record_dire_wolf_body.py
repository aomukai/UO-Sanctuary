#!/usr/bin/env python3
"""Bind the saved Fenris body and approved corpse transactions to native acceptance."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/dire-wolf-body'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {p for p in inputs['sourceInputs'] if p.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        path = ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')
        if name == 'Data/Sanctuary/Balance/skill-effects.json':
            # Only the Dire placeholder description changed after the final
            # mechanics run. Retain the exact loaded register and prove that
            # all other fields/rows are identical, instead of rerunning combat
            # to certify a prose-only asset note.
            tested = STAGE / 'tested-skill-effects.json'
            assert sha(tested) == digest
            before, after = json.loads(tested.read_text()), json.loads(path.read_text())
            for rows in [before, after]:
                next(x for x in rows if x['talentId'] == 'druid.summondirewolf').pop('placeholder')
            assert before == after
        else:
            assert sha(path) == digest, name
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
    assert log.count(': passed') == 205
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('dire-wolf-ai-check: passed') == 1
    assert '3678native ordered-service/memory comparisons' in log
    for name in ['spirit-wolf-mode', 'spirit-wolf-body', 'spirit-wolf-casting', 'barbs-casting', 'barbs-world', 'wolverine-casting', 'oak-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for family in ['spirit-wolf-right', 'spirit-wolf-quick', 'barbs-right', 'barbs-quick', 'oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick']:
        assert log.count(family + '-check: passed') == log.count(family + '-result: passed') == 3, family
    for family in ['spirit-wolf-world-live', 'spirit-wolf-hit-live', 'spirit-wolf-rate-live', 'spirit-wolf-slow-live', 'barbs-world-live', 'oak-world-live', 'wolverine-world-live']:
        assert log.count(family + '-result: passed') == 3, family
    assert 'Dire saved body and type-independent corpse transactions pass' in (STAGE / 'stats-run.log').read_text()
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
    frozen = json.loads(subprocess.check_output(['git', 'show', 'a1721cb:tools/reference/dire-wolf-ai-native-evidence.json'], cwd=ROOT))
    for name, digest in frozen['artifacts'].items():
        actual = sha(ROOT / name) if name.startswith('.local/') else hashlib.sha256(subprocess.check_output(['git', 'show', 'a1721cb:' + name], cwd=ROOT)).hexdigest()
        assert actual == digest, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 170 and sum(x['characterClass'] == 'Druid' for x in handlers) == 14
    assert status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summondirewolf')['implementation'] != 'handler-present'
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'audit-native-checks.log', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'dire-wolf-body-fixtures.json', 'corpse-explosion-cast-fixtures.json', 'tested-skill-effects.json']]
    artifacts += [source_path, fixture, ROOT / '.local/class-skills/dire-wolf-ai-source-probe.cpp', ROOT / '.local/class-skills/dire-wolf-ai-sanitizer.log']
    artifacts += [ROOT / name for name in ['src/Sanctuary.Content/DruidDireWolfAi.cs', 'tests/Sanctuary.Integration/DireWolfAiChecks.cs', 'tests/check_dire_wolf_ai.py', 'tools/record_dire_wolf_ai.py', 'tools/probe_dire_wolf_ai_reference.py', 'tools/reference/dire-wolf-corpse-deviation.json', 'src/Sanctuary.Content/DruidDireWolfBody.cs', 'src/Sanctuary.Content/DruidDireWolfCorpses.cs', 'src/Sanctuary.Content/CorpseSkillRules.cs', 'src/Sanctuary.Content/CreatureProfiles.cs', 'tests/Sanctuary.Integration/DireWolfBodyChecks.cs', 'tests/check_dire_wolf_body.py', 'tests/export_runtime_audits.py', 'tools/record_dire_wolf_body.py', 'tools/build_class_skills.py', 'data/Sanctuary/Balance/skill-effects.json']]
    result = dict(referenceVersion='3.3.93847', legacyCommit=source['legacyCommit'], handlerEnabled=False, installed=False,
        handlers=170, druidHandlers=14, completedClasses=[], goalComplete=False,
        verified=dict(sourceContexts=1226, nativeOrderedComparisons=3678, difficultyColumns=3, passMarkers=205, boots=3, retainedReloads=2),
        testedAssemblies=inputs['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Prepared Fenris body has immutable captured birth/passive ranks, fractional rank1 life88473FP,54FP recovery, source species/defense/resistances and vulnerability. It keeps the existing serialized SkeletonGuardian base chain and suppresses native skeleton AI/damage. Invalid restores fail atomically; real gear changes only affect fresh captures.',
            'Actual ownership/history parking, orphan deletion, offline frame discard, one-frame recovery and owner-facet difficulty rescale preserve captured ranks and life percentage. Two real server reloads retain the saved body and consumed/fresh corpse states.',
            'Approved type-independent corpse route reuses shared native access/map/loot and reservation checks. Actual skeletal and fleshy deaths can be consumed without selectable/source/base-life/seed data. Existing generic corpse skills still require selectable metadata. Owned, shattered, reserved, harvested and cross-map controls refuse feeding.',
            'Reservation and loot-safe consumption precede the explicit rage-allocation callback. A failed buff allocation still consumes; repeated/competing reservations fail; retained Gold survives and an empty consumed corpse uses shared cleanup. No direct life/mana write is invented.',
            '205 retained markers pass across three boots and two actual reloads, including existing player corpse-explosion casts, Fenris policy and wolf/spirit consumers. Separate real runtime audits preserve all573 named decisions; all70 Python checks pass. Both builds have zero warnings/errors. Only the Dire asset-placeholder prose changed after the mechanics run; the exact tested register is retained and all remaining rows/fields compare equal.',
        ], limits=[
            'No automatic Dire world controller, corpse acquisition/reach/animation dispatch, actual500frame/+100percent rage stat consumer, physical combat or learned placement/payment/input is connected. The rage callback is an explicit service, not a completed rage implementation.',
            'Facet rescale is verified separately from travel: this body-only fixture uses shared suspend/resume placement. Automatic path/motion/teleport and exact current timing/RNG/geometry require the later world controller. Nonzero physical-sunder delivery remains unsupported.',
            'No class is complete and no current executable parity certificate or installed candidate rollout is claimed. Main2a6eec9,original saves/configuration/client and the earlier accepted packages remain preserved. Earlier AI package/evidence is frozen at a1721cb.',
            'The body uses silent UO0xE1 named Dire Wolf [placeholder]. Final WF/corpse/rage/contact/audio effects and separate talent/right/quick icons remain pending.',
        ])
    path = ROOT / 'tools/reference/dire-wolf-body-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Dire body/corpse component: 205 markers, 3 boots, 2 actual reloads, 70 Python checks; automatic world/rage/combat/casting pending, installed build unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
